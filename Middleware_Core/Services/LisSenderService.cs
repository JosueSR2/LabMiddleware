using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Middleware_Core.Models;

namespace Middleware_Core.Services
{
    public class LisSenderService
    {
        private readonly HttpClient _httpClient;

        public LisSenderService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task SendAsync(LabResult result, string lisUrl)
        {
            try
            {
                var json = JsonSerializer.Serialize(result);

                var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(lisUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✔ Enviado correctamente: {result.SampleId}");
                }
                else
                {
                    Console.WriteLine($"❌ Error enviando a LIS: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LisSenderService ERROR] {ex.Message}");
            }
        }
    }
}


