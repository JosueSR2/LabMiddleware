using System;
using System.IO;
using System.IO.Ports;
using Middleware.Core.Parsers;

namespace Middleware.Core.Parsers
{
    public static class ParserFactory
    {
        public static IAnalyzerParser GetParser(string fileName, string rawMessage)
        {
            // Detectar por contenido (más robusto que por extensión)

            if (rawMessage.Contains("MSH|"))
            {
                Console.WriteLine("[ParserFactory] HL7 detected");
                return new Hl7Parser();
            }

            if (rawMessage.Contains("H|\\^&") || rawMessage.StartsWith("H|"))
            {
                Console.WriteLine("[ParserFactory] ASTM detected");
                return new AstmParser();
            }

            throw new NotSupportedException("Unknown analyzer format");
        }
    }
}


