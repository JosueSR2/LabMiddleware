using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Middleware.Core.Services
{
    public class LisSenderService
    {
        private readonly HttpClient _httpClient;

        public LisSenderService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task SendAsync(string hl7Message, string url)
        {
            var content = new StringContent(hl7Message, Encoding.UTF8, "application/hl7-v2");
            await _httpClient.PostAsync(url, content);
        }
    }
}
