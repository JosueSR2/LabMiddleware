using System;
using System.Collections.Generic;
using System.Text;
using Middleware_Core.Models;

namespace Middleware_Core.Builders
{
    public static class Hl7Builder
    {
        public static string Build(List<LabResult> results)
        {
            if (results == null || results.Count == 0)
                throw new ArgumentException("At least one LabResult is required to build an HL7 message.", nameof(results));

            var sb = new StringBuilder();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var messageControlId = Guid.NewGuid().ToString("N")[..20];
            var first = results[0];

            sb.Append($"MSH|^~\\&|Middleware|Laboratory|OpenELIS|Global2|{timestamp}||ORU^R01|{Escape(messageControlId)}|P|2.5.1\r");
            sb.Append($"PID|1||{Escape(first.PatientId)}||{Escape(first.PatientName)}\r");
            sb.Append($"OBR|1|{Escape(first.SampleId)}||LAB^Panel\r");

            int index = 1;
            foreach (var result in results)
            {
                var testCode = string.IsNullOrWhiteSpace(result.TestCode) ? "UNKNOWN" : result.TestCode;
                var testName = string.IsNullOrWhiteSpace(result.TestName) ? testCode : result.TestName;
                var flag = string.IsNullOrWhiteSpace(result.Flag) ? "F" : result.Flag;

                sb.Append($"OBX|{index}|ST|{Escape(testCode)}^{Escape(testName)}||{Escape(result.Value)}|{Escape(result.Units)}|||{Escape(flag)}|||||{timestamp}\r");
                index++;
            }

            return sb.ToString();
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value
                .Replace(@"\", @"\E\")
                .Replace("|", @"\F\")
                .Replace("^", @"\S\")
                .Replace("&", @"\T\")
                .Replace("~", @"\R\")
                .Trim();
        }
    }
}
