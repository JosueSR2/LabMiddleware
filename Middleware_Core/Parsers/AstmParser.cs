using System;
using System.Collections.Generic;
using Middleware.Core.Models;

namespace Middleware.Core.Parsers
{
    public class AstmParser : IAnalyzerParser
    {
        public List<LabResult> Parse(string rawMessage)
        {
            var results = new List<LabResult>();

            if (string.IsNullOrWhiteSpace(rawMessage))
                return results;

            // Separar líneas
            var lines = rawMessage.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            string sampleId = string.Empty;
            string patientName = string.Empty;

            foreach (var line in lines)
            {
                var fields = line.Split('|');

                if (fields.Length == 0) continue;

                switch (fields[0])
                {
                    case "O": // Orden
                        sampleId = fields.Length > 2 ? fields[2] : string.Empty;
                        patientName = fields.Length > 1 ? fields[1] : string.Empty;
                        break;

                    case "R": // Resultado de prueba
                        var labResult = new LabResult
                        {
                            SampleId = sampleId,
                            PatientName = patientName,
                            TestCode = fields.Length > 2 ? fields[2] : string.Empty,
                            TestName = fields.Length > 2 ? fields[2] : string.Empty,
                            Value = fields.Length > 3 ? fields[3] : string.Empty,
                            Units = fields.Length > 4 ? fields[4] : string.Empty,
                            Flag = fields.Length > 7 ? fields[7] : string.Empty,
                            SourceMachine = "ASTM Machine"
                        };
                        results.Add(labResult);
                        break;

                    case "L": // Fin de registro
                        // Podríamos hacer algo si se necesita
                        break;
                }
            }

            return results;
        }
    }
}
