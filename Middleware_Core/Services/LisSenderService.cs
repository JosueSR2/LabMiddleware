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
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.Resilience.HttpTimeoutSeconds));
        }

        public async Task<LisSendResult> SendAsync(LabResult result, string lisUrl, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString("N");

            if (_options.LisSecurity.RequireTls && !lisUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                StructuredLog.Error("lis.security.tls_required", new { correlationId, lisUrl });
                return new LisSendResult(false, "TLS is required by configuration.");
            }

            if (_circuitOpenUntilUtc > DateTime.UtcNow)
            {
                StructuredLog.Error("lis.circuit.open", new { correlationId, openUntilUtc = _circuitOpenUntilUtc });
                return new LisSendResult(false, $"Circuit is open until {_circuitOpenUntilUtc:O}");
            }

            try
            {
                var payload = BuildPayload(result);
                var contentType = string.IsNullOrWhiteSpace(_options.LisDelivery.ContentType)
                    ? "text/plain"
                    : _options.LisDelivery.ContentType;
                var mediaType = contentType.Split(';', 2)[0].Trim();

                var request = new HttpRequestMessage(HttpMethod.Post, lisUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, mediaType)
                };

                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                if (!string.IsNullOrWhiteSpace(_options.LisSecurity.BearerToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.LisSecurity.BearerToken);
                }
                else if (!string.IsNullOrWhiteSpace(_options.LisSecurity.BasicUsername))
                {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.LisSecurity.BasicUsername}:{_options.LisSecurity.BasicPassword}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _consecutiveFailures = 0;
                    StructuredLog.Info("lis.send.success", new { correlationId, sampleId = result.SampleId, status = (int)response.StatusCode });
                    return new LisSendResult(true, StatusCode: (int)response.StatusCode);
                }

                var failureBody = await ReadBodySnippetAsync(response, cancellationToken);
                var reason = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim();
                if (!string.IsNullOrWhiteSpace(failureBody))
                    reason = $"{reason} | body: {failureBody}";

                RegisterFailure(correlationId, reason);
                return new LisSendResult(false, reason, (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                LisPayloadMode.JsonLabResult => JsonSerializer.Serialize(result),
                LisPayloadMode.Hl7FromResult => Hl7Builder.Build(new List<LabResult> { result }),
                LisPayloadMode.RawMessage => !string.IsNullOrWhiteSpace(result.RawMessage)
                    ? ApplyHl7SendingApplication(result.RawMessage, result.SourceMachine)
                    : throw new InvalidOperationException("RawMessage is empty while LisDelivery.PayloadMode=RawMessage."),
                _ => throw new InvalidOperationException($"Unsupported payload mode: {_options.LisDelivery.PayloadMode}")
            };
        }

        private static string ApplyHl7SendingApplication(string rawMessage, string? sourceMachine)
        {
            if (string.IsNullOrWhiteSpace(rawMessage) || string.IsNullOrWhiteSpace(sourceMachine))
                return rawMessage;

            var lineBreak = rawMessage.Contains("\r\n", StringComparison.Ordinal)
                ? "\r\n"
                : rawMessage.Contains('\r') ? "\r" : "\n";
            var segments = rawMessage.Split(new[] { lineBreak }, StringSplitOptions.None);

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                if (!segment.StartsWith("MSH|", StringComparison.Ordinal))
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

        private static async Task<string> ReadBodySnippetAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(body))
                    return string.Empty;

                body = body.Replace("\r", " ").Replace("\n", " ").Trim();
                return body.Length <= 300 ? body : $"{body[..300]}...";
            }
            catch
            {
                return string.Empty;
            }
        }

        private void RegisterFailure(string correlationId, string reason)
        {
            _consecutiveFailures++;
            StructuredLog.Error("lis.send.failure", new { correlationId, reason, consecutiveFailures = _consecutiveFailures });

            if (_consecutiveFailures >= _options.Resilience.CircuitBreakerFailureThreshold)
            {
                _circuitOpenUntilUtc = DateTime.UtcNow.AddSeconds(_options.Resilience.CircuitBreakerBreakSeconds);
                _consecutiveFailures = 0;
                StructuredLog.Error("lis.circuit.opened", new { correlationId, openUntilUtc = _circuitOpenUntilUtc });
            }
        }
    }
}
