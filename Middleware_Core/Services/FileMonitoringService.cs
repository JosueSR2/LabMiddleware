using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Middleware_Core.Models;
using Middleware_Core.Parsers;

namespace Middleware_Core.Services
{
    public class FileMonitoring
    {
        private readonly HttpClient _httpClient;

        public FileMonitoring(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task ProcessFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            var rawMessage = await File.ReadAllTextAsync(filePath);

            // 🔥 Aquí usamos tu ParserFactory
            IAnalyzerParser parser = ParserFactory.GetParser(filePath, rawMessage);

            var results = parser.Parse(rawMessage);

            foreach (var result in results)
            {
                await SendAsync(result);
            }
        }

        private async Task SendAsync(LabResult result)
        {
            var json = JsonSerializer.Serialize(new
            {
                sampleId = result.SampleId,
                patientName = result.PatientName,
                testCode = result.TestCode,
                testName = result.TestName,
                value = result.Value,
                units = result.Units,
                flag = result.Flag,
                timestamp = result.Timestamp,
                source = result.SourceMachine
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "http://openelis/api/results",
                content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error enviando resultado: {response.StatusCode}");
            }
            else
            {
                Console.WriteLine("Resultado enviado correctamente.");
            }
        }
    }
}

