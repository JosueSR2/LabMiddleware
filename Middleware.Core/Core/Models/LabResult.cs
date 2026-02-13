namespace Middleware.Core.Models
{
    public class LabResult
    {
        public string SampleId { get; set; }
        public string PatientName { get; set; }
        public string TestCode { get; set; }
        public string TestName { get; set; }
        public string Value { get; set; }
        public string Units { get; set; }
        public string Flag { get; set; }  // F = Final, C = Corrected, P = Preliminary
        public DateTime Timestamp { get; set; }
        public string SourceMachine { get; set; }
    }
}
