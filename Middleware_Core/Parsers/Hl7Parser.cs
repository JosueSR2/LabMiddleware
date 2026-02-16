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

            foreach (var line in lines)
            {
                var fields = line.Split('|');
                if (fields.Length == 0) continue;

                // PID segment: Sample/Patient info
                if (fields[0] == "PID")
                {
                    sampleId = fields.Length > 3 ? fields[3] : "";
                    patientName = fields.Length > 5 ? fields[5] : "";
                }

                // OBX segment: Lab result
                if (fields[0] == "OBX")
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
            }

            return results;
        }
    }
}
