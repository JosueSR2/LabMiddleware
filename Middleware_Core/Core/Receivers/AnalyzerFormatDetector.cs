namespace Middleware_Core.Parsers
{
    public static class AnalyzerFormatDetector
    {
        public static string Detect(string content)
        {
            if (content.Contains("MSH|"))
                return "HL7";

            if (content.Contains("O|") && content.Contains("R|"))
                return "ASTM";

            if (content.Contains(","))
                return "CSV";

            if (content.Contains("\t"))
                return "TAB";

            return "UNKNOWN";
        }
    }
}
