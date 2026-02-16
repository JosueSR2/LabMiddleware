namespace Middleware_Core.Models
{
    public class LabResult
    {
        public Guid Id { get; set; }
            
        public string? SampleId { get; set; }
        public string PatientName { get; set; }
        public string PatientId { get; set; }
        public string TestCode { get; set; }
        public string TestName { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Units { get; set; }
        public string Flag { get; set; } 
        public string AnalyzerId { get; set; }
        public string RawMessage { get; set; }
        public string Status { get; set; } = "Pending";
        public int RetryCount { get; set; } = 0;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SourceMachine { get; set; }
    }
}
