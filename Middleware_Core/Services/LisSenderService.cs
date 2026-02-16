using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Middleware_Core.Core.Repositories;

namespace Middleware_Core.Services
{
    public class LisSenderService : BackgroundService
    {
        private readonly HttpClient _httpClient;
        private readonly IResultRepository _repository;

        public LisSenderService(HttpClient httpClient, IResultRepository repository)
        {
            _httpClient = httpClient;
            _repository = repository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pending = await _repository.GetPendingAsync();

                foreach (var result in pending)
                {
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

                    var response = await _httpClient.PostAsync(
                        "http://openelis/api/results",
                        content,
                        stoppingToken);

                    if (response.IsSuccessStatusCode)
                        await _repository.MarkAsSent(result.Id);
                    else
                        await _repository.IncrementRetry(result.Id);
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}

