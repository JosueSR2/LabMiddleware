using Middleware_Core.Models;
using System;
using System.Collections.Generic;

namespace Middleware_Core.Parsers
{
    public class Hl7Parser : IAnalyzerParser
    {
        public List<LabResult> Parse(string rawMessage)
        {
            var results = new List<LabResult>();

            if (string.IsNullOrWhiteSpace(rawMessage))
                return results;

            var lines = rawMessage.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string sampleId = "";
            string patientName = "";
            var lines = rawMessage.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            string sampleId = string.Empty;
            string patientName = string.Empty;

            foreach (var line in lines)
            {
                var fields = line.Split('|');
                if (fields.Length == 0) continue;
                if (fields.Length == 0)
                    continue;

                // PID segment: Sample/Patient info
                if (fields[0] == "PID")
                {
                    sampleId = fields.Length > 3 ? fields[3] : "";
                    patientName = fields.Length > 5 ? fields[5] : "";
                    sampleId = SafeField(fields, 3);
                    patientName = SafeField(fields, 5);
                    continue;
                }

                // OBX segment: Lab result
                if (fields[0] == "OBX")
                if (fields[0] != "OBX")
                    continue;

                var obx3 = SafeField(fields, 3);
                var obx3Parts = obx3.Split('^');
                var testCode = obx3Parts.Length > 0 ? obx3Parts[0] : string.Empty;
                var testName = obx3Parts.Length > 1 ? obx3Parts[1] : testCode;

                results.Add(new LabResult
                {
                    results.Add(new LabResult
                    {
                        SampleId = sampleId,
                        PatientName = patientName,
                        TestCode = fields.Length > 3 ? fields[3].Split('^')[0] : "",
                        TestName = fields.Length > 3 ? fields[3].Split('^')[1] : "",
                        Value = fields.Length > 5 ? fields[5] : "",
                        Units = fields.Length > 6 ? fields[6] : "",
                        Flag = fields.Length > 8 ? fields[8] : "F",
                        Timestamp = DateTime.Now,
                        SourceMachine = "HL7"
                    });
                }
                    SampleId = sampleId,
                    PatientName = patientName,
                    TestCode = testCode,
                    TestName = testName,
                    Value = SafeField(fields, 5),
                    Units = SafeField(fields, 6),
                    Flag = string.IsNullOrWhiteSpace(SafeField(fields, 8)) ? "F" : SafeField(fields, 8),
                    Timestamp = DateTime.UtcNow,
                    SourceMachine = "HL7"
                });
            }

            return results;
        }

        private static string SafeField(string[] fields, int index)
        {
            if (index < 0 || index >= fields.Length)
                return string.Empty;

            return fields[index] ?? string.Empty;
        }
    }
}

