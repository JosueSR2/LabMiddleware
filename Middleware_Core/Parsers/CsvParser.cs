using Middleware_Core.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Middleware_Core.Parsers
{
    public class CsvParser : IAnalyzerParser
    {
        public List<LabResult> Parse(string rawMessage)
        {
            var results = new List<LabResult>();
            using (var reader = new StringReader(rawMessage))
            {
                string line;
                string sampleId = "";
                string patientName = "";

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var fields = line.Split(',');

                    // Suponiendo CSV: SampleId,PatientName,TestCode,TestName,Value,Units,Flag
                    if (fields.Length < 6) continue;

                    sampleId = fields[0];
                    patientName = fields[1];

                    results.Add(new LabResult
                    {
                        SampleId = sampleId,
                        PatientName = patientName,
                        TestCode = fields[2],
                        TestName = fields[3],
                        Value = fields[4],
                        Units = fields[5],
                        Flag = fields.Length > 6 ? fields[6] : "F",
                        Timestamp = DateTime.Now,
                        SourceMachine = "CSV"
                    });
                }
            }

            return results;
        }
    }
}
