using Middleware.Core.Models;
using System;
using System.Collections.Generic;

namespace Middleware.Core.Parsers
{
    public class DimensionParser : IAnalyzerParser
    {
        public List<LabResult> Parse(string rawMessage)
        {
            var results = new List<LabResult>();

            // Limpiar STX, ETX y FS
            rawMessage = rawMessage
                .Replace(((char)0x02).ToString(), "")
                .Replace(((char)0x03).ToString(), "");

            var fields = rawMessage.Split((char)0x1C); // FS

            if (fields.Length < 3) return results;

            string sampleId = fields[2];

            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] == "GLUC" || fields[i] == "NA" || fields[i] == "K" || fields[i] == "CRE2")
                {
                    results.Add(new LabResult
                    {
                        SampleId = sampleId,
                        TestCode = fields[i],
                        TestName = fields[i],
                        Value = fields[i + 1],
                        Units = fields[i + 2],
                        Flag = "F",
                        Timestamp = DateTime.Now,
                        SourceMachine = "Dimension"
                    });
                }
            }

            return results;
        }
    }
}
