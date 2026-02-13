using System;
using System.Collections.Generic;

namespace Middleware.Core.Parsers
{
    public static class ParserFactory
    {
        /// <summary>
        /// Detecta el parser adecuado según el contenido del mensaje o la extensión del archivo
        /// </summary>
        public static IAnalyzerParser GetParser(string fileName, string rawMessage)
        {
            string ext = Path.GetExtension(fileName).ToLower();

            if (ext == ".hl7" || rawMessage.StartsWith("H|"))
                return new Hl7Parser();
            else if (ext == ".astm" || rawMessage.StartsWith(((char)0x02).ToString()))
                return new DimensionParser();
            else if (ext == ".csv" || rawMessage.Contains(","))
                return new CsvParser();

            throw new NotSupportedException($"No se encontró un parser compatible para {fileName}");
        }

    }
}
