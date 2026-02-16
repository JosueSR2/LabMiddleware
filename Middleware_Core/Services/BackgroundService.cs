using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Middleware.Core.Core.Services
{
    public class LisSenderService : BackgroundService
    {
        private readonly ILogger<LisSenderService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IResultRepository _repository;

        private const string OpenElisEndpoint = "https://your-openelis-server/api/results";
        private const string Username = "your-username";
        private const string Password = "your-password";

        public LisSenderService(
            ILogger<LisSenderService> logger,
            IHttpClientFactory httpClientFactory,
            IResultRepository repository)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _repository = repository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LisSenderService iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var pendingResults = await _repository.GetPendingAsync();

                    foreach (var result in pendingResults)
                    {
                        await SendResultAsync(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error general en LisSenderService");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }

            _logger.LogInformation("LisSenderService detenido.");
        }

        private async Task SendResultAsync(LabResult result)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var byteArray = Encoding.ASCII.GetBytes($"{Username}:{Password}");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                var json = JsonSerializer.Serialize(new
                {
                    sampleId = result.SampleId,
                    patientId = result.PatientId,
                    analyzerId = result.AnalyzerId,
                    testCode = result.TestCode,
                    value = result.Value,
                    unit = result.Unit
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(OpenElisEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    await _repository.MarkAsSent(result.Id);
                    _logger.LogInformation($"Resultado {result.Id} enviado correctamente.");
                }
                else
                {
                    await _repository.IncrementRetry(result.Id);
                    _logger.LogWarning($"Error enviando resultado {result.Id}. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                await _repository.IncrementRetry(result.Id);
                _logger.LogError(ex, $"Excepción enviando resultado {result.Id}");
            }
        }
    }
}
