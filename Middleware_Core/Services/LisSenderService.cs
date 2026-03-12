using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Middleware_Core.Builders;
using Middleware_Core.Configuration;
using Middleware_Core.Models;

namespace Middleware_Core.Services
{
    public class LisSenderService
    {
        private readonly HttpClient _httpClient;
        private readonly MiddlewareOptions _options;
        private int _consecutiveFailures;
        private DateTime _circuitOpenUntilUtc = DateTime.MinValue;

        public record LisSendResult(bool Success, string? Error = null, int? StatusCode = null);

        public LisSenderService(HttpClient httpClient, MiddlewareOptions options)
        {
            _httpClient = httpClient;
            _options = options;

            _httpClient.Timeout =
                TimeSpan.FromSeconds(Math.Max(1, options.Resilience.HttpTimeoutSeconds));
        }

        public Task<LisSendResult> SendAsync(
            LabResult result,
            CancellationToken cancellationToken = default)
        {
            var lisUrl = ResolveLisUrl(result);
            return SendAsync(result, lisUrl, cancellationToken);
        }

        public async Task<LisSendResult> SendAsync(
            LabResult result,
            string lisUrl,
            CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString("N");

            if (_options.LisSecurity.RequireTls &&
                !lisUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                StructuredLog.Error("lis.security.tls_required", new { correlationId, lisUrl });

                return new LisSendResult(false, "TLS is required by configuration.");
            }

            if (_circuitOpenUntilUtc > DateTime.UtcNow)
            {
                StructuredLog.Error("lis.circuit.open",
                    new { correlationId, openUntilUtc = _circuitOpenUntilUtc });

                return new LisSendResult(false,
                    $"Circuit is open until {_circuitOpenUntilUtc:O}");
            }

            try
            {
                var payload = BuildPayload(result);

                var contentType = ResolveContentType(result);

                var mediaType = contentType.Split(';', 2)[0].Trim();

                var request = new HttpRequestMessage(HttpMethod.Post, lisUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, mediaType)
                };

                // StringContent already sets charset=utf-8. Only override when explicitly provided.
                if (contentType.Contains("charset=", StringComparison.OrdinalIgnoreCase))
                {
                    request.Content.Headers.ContentType =
                        MediaTypeHeaderValue.Parse(contentType);
                }

                // Prefer configured Accept; fall back to */*.
                var acceptHeader = string.IsNullOrWhiteSpace(_options.LisDelivery.Accept)
                    ? "*/*"
                    : _options.LisDelivery.Accept;
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(acceptHeader));

                ApplyAuthorization(request);

                StructuredLog.Info("lis.send.request", new
                {
                    correlationId,
                    lisUrl,
                    contentType,
                    messageFormat = DetectFormat(payload),
                    payloadPreview = payload.Length > 200
                        ? payload[..200]
                        : payload
                });

                var response =
                    await _httpClient.SendAsync(request, cancellationToken);

                var body =
                    await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _consecutiveFailures = 0;

                    StructuredLog.Info("lis.send.success", new
                    {
                        correlationId,
                        sampleId = result.SampleId,
                        status = (int)response.StatusCode
                    });

                    return new LisSendResult(true,
                        StatusCode: (int)response.StatusCode);
                }

                var reason =
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

                if (!string.IsNullOrWhiteSpace(body))
                    reason += $" | body: {body}";

                RegisterFailure(correlationId, reason);

                return new LisSendResult(false, reason,
                    (int)response.StatusCode);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                var reason = "Delivery canceled by shutdown token.";

                RegisterFailure(correlationId, reason);

                return new LisSendResult(false, reason);
            }
            catch (TaskCanceledException)
            {
                var reason = "HTTP timeout while sending to LIS.";

                RegisterFailure(correlationId, reason);

                return new LisSendResult(false, reason);
            }
            catch (Exception ex)
            {
                RegisterFailure(correlationId, ex.Message);

                return new LisSendResult(false, ex.Message);
            }
        }

        private string BuildPayload(LabResult result)
        {
            return _options.LisDelivery.PayloadMode switch
            {
                LisPayloadMode.JsonLabResult =>
                    JsonSerializer.Serialize(result),

                LisPayloadMode.Hl7FromResult =>
                    Hl7Builder.Build(new List<LabResult> { result }),

                LisPayloadMode.RawMessage =>
                    !string.IsNullOrWhiteSpace(result.RawMessage)
                        ? ProcessRawMessage(result)
                        : throw new InvalidOperationException(
                            "RawMessage is empty while LisDelivery.PayloadMode=RawMessage."),

                _ => throw new InvalidOperationException(
                    $"Unsupported payload mode: {_options.LisDelivery.PayloadMode}")
            };
        }

        private string ProcessRawMessage(LabResult result)
        {
            if (IsHl7(result.RawMessage))
                return ApplyHl7SendingApplication(result.RawMessage,
                    !string.IsNullOrWhiteSpace(result.AnalyzerId)
                        ? result.AnalyzerId
                        : result.SourceMachine);

            return result.RawMessage;
        }

        private static bool IsHl7(string message)
        {
            return message.StartsWith("MSH|");
        }

        private static string DetectFormat(string payload)
        {
            if (payload.StartsWith("MSH|"))
                return "HL7";

            if (payload.StartsWith("H|") || payload.StartsWith("1H|"))
                return "ASTM";

            return "UNKNOWN";
        }

        private string ResolveContentType(LabResult result)
        {
            if (!string.IsNullOrWhiteSpace(_options.LisDelivery.ContentType))
                return _options.LisDelivery.ContentType;

            if (IsHl7(result.RawMessage))
                return "application/hl7-v2";

            return "text/plain";
        }

        private string ResolveLisUrl(LabResult result)
        {
            var fallback = _options.LisUrl;
            var astmUrl = string.IsNullOrWhiteSpace(_options.LisUrlAstm) ? fallback : _options.LisUrlAstm;
            var hl7Url = string.IsNullOrWhiteSpace(_options.LisUrlHl7) ? fallback : _options.LisUrlHl7;

            if (_options.LisDelivery.PayloadMode == LisPayloadMode.Hl7FromResult)
                return hl7Url;

            if (_options.LisDelivery.PayloadMode != LisPayloadMode.RawMessage)
                return fallback;

            var raw = result.RawMessage ?? string.Empty;
            var trimmed = raw.TrimStart();

            if (trimmed.StartsWith("MSH|", StringComparison.Ordinal))
                return hl7Url;

            // OpenELIS ASTM reader expects H| header, but route other record types as ASTM too.
            if (trimmed.StartsWith("H|", StringComparison.Ordinal) ||
                trimmed.StartsWith("P|", StringComparison.Ordinal) ||
                trimmed.StartsWith("O|", StringComparison.Ordinal) ||
                trimmed.StartsWith("R|", StringComparison.Ordinal) ||
                trimmed.StartsWith("L|", StringComparison.Ordinal))
                return astmUrl;

            return fallback;
        }

        private void ApplyAuthorization(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(_options.LisSecurity.BearerToken))
            {
                var token = _options.LisSecurity.BearerToken
                    .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
            else if (!string.IsNullOrWhiteSpace(_options.LisSecurity.BasicUsername))
            {
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{_options.LisSecurity.BasicUsername}:{_options.LisSecurity.BasicPassword}"));

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
            }
        }

        private static string ApplyHl7SendingApplication(
            string rawMessage,
            string? sourceMachine)
        {
            if (string.IsNullOrWhiteSpace(rawMessage) ||
                string.IsNullOrWhiteSpace(sourceMachine))
                return rawMessage;

            var lineBreak = rawMessage.Contains("\r\n")
                ? "\r\n"
                : rawMessage.Contains('\r') ? "\r" : "\n";

            var segments =
                rawMessage.Split(new[] { lineBreak },
                    StringSplitOptions.None);

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];

                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                if (!segment.StartsWith("MSH|"))
                    break;

                var fields = segment.Split('|');

                if (fields.Length > 2)
                {
                    fields[2] = sourceMachine.Trim();

                    segments[i] = string.Join("|", fields);

                    return string.Join(lineBreak, segments);
                }

                break;
            }

            return rawMessage;
        }

        private void RegisterFailure(string correlationId, string reason)
        {
            _consecutiveFailures++;

            StructuredLog.Error("lis.send.failure", new
            {
                correlationId,
                reason,
                consecutiveFailures = _consecutiveFailures
            });

            if (_consecutiveFailures >=
                _options.Resilience.CircuitBreakerFailureThreshold)
            {
                _circuitOpenUntilUtc =
                    DateTime.UtcNow.AddSeconds(
                        _options.Resilience.CircuitBreakerBreakSeconds);

                _consecutiveFailures = 0;

                StructuredLog.Error("lis.circuit.opened", new
                {
                    correlationId,
                    openUntilUtc = _circuitOpenUntilUtc
                });
            }
        }
    }
}
