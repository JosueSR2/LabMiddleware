namespace Middleware_Core.Models
{
    public class LabResult
    {
        public Guid Id { get; set; }

        public string? SampleId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Units { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;
        public string AnalyzerId { get; set; } = string.Empty;
        public string RawMessage { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public int RetryCount { get; set; } = 0;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SourceMachine { get; set; } = string.Empty;
    }
}
