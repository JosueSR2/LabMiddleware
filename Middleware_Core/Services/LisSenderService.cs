using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        public LisSenderService(HttpClient httpClient, MiddlewareOptions options)
        {
            _httpClient = httpClient;
            _options = options;
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.Resilience.HttpTimeoutSeconds));
        }
        public async Task<bool> SendAsync(LabResult result, string lisUrl)
        {
            var correlationId = Guid.NewGuid().ToString("N");

            if (_options.LisSecurity.RequireTls && !lisUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                StructuredLog.Error("lis.security.tls_required", new { correlationId, lisUrl });
                return false;
            }

            if (_circuitOpenUntilUtc > DateTime.UtcNow)
            {
                StructuredLog.Error("lis.circuit.open", new { correlationId, openUntilUtc = _circuitOpenUntilUtc });
                return false;
            }
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, lisUrl)
                {
                    Content = new StringContent(JsonSerializer.Serialize(result), Encoding.UTF8, "application/json")
                };
                if (!string.IsNullOrWhiteSpace(_options.LisSecurity.BearerToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.LisSecurity.BearerToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _consecutiveFailures = 0;
                    StructuredLog.Info("lis.send.success", new { correlationId, sampleId = result.SampleId, status = (int)response.StatusCode });
                    return true;
                }

                RegisterFailure(correlationId, $"HTTP {(int)response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                RegisterFailure(correlationId, ex.Message);
                return false;
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
