using System.Text.Json;

namespace Middleware_Core.Services
{
    public static class StructuredLog
    {
        public static void Info(string eventName, object data) => Write("INFO", eventName, data);
        public static void Error(string eventName, object data) => Write("ERROR", eventName, data);

        private static void Write(string level, string eventName, object data)
        {
            var payload = new
            {
                ts = DateTime.UtcNow,
                level,
                eventName,
                data
            };

            Console.WriteLine(JsonSerializer.Serialize(payload));
        }
    }
}
