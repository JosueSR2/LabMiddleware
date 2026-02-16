using System.Collections.Generic;
using Middleware_Core.Models;

namespace Middleware_Core.Parsers
{
    public class GenericTextParser : IAnalyzerParser
    {
        public List<LabResult> Parse(string rawMessage)
        {
            var results = new List<LabResult>();
            var lines = rawMessage.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains("="))
                {
                    var parts = line.Split('=');

                    results.Add(new LabResult
                    {
                        TestCode = parts[0].Trim(),
                        Value = parts[1].Trim()
                    });
                }
            }

            return results;
        }
    }
}

