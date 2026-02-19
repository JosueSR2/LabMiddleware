using Middleware_Core.Models;

namespace Middleware_Core.Outbox
{
    public class OutboxRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Fingerprint { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public LabResult Payload { get; set; } = new();
        public string Status { get; set; } = "Pending";
        public int RetryCount { get; set; }
        public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
        public string? LastError { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
