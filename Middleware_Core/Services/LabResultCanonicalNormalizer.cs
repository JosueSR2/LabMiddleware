using Middleware_Core.Models;

namespace Middleware_Core.Services
{
    public static class LabResultCanonicalNormalizer
    {
        public static LabResult Normalize(LabResult input, string defaultSource)
        {
            input.SampleId = Clean(input.SampleId);
            input.PatientName = Clean(input.PatientName);
            input.PatientId = Clean(input.PatientId);
            input.TestCode = Clean(input.TestCode);
            input.TestName = Clean(input.TestName);
            input.Value = Clean(input.Value);

            var unit = Clean(input.Units);
            if (string.IsNullOrWhiteSpace(unit))
                unit = Clean(input.Unit);

            input.Units = unit;
            input.Unit = unit;
            input.Flag = string.IsNullOrWhiteSpace(input.Flag) ? "F" : input.Flag.Trim();
            input.SourceMachine = string.IsNullOrWhiteSpace(input.SourceMachine) ? defaultSource : input.SourceMachine.Trim();
            input.Timestamp = input.Timestamp == default ? DateTime.UtcNow : input.Timestamp;

            return input;
        }

        private static string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}