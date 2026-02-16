using Middleware_Core.Parsers;
using Middleware_Core.Builders;

namespace Middleware_Core.Services
{
    public class AnalyzerMessageProcessor
    {
        private readonly IAnalyzerParser _parser;

        public AnalyzerMessageProcessor(IAnalyzerParser parser)
        {
            _parser = parser;
        }

        public string Process(string rawMessage)
        {
            var results = _parser.Parse(rawMessage);
            return Hl7Builder.Build(results);
        }
    }
}
