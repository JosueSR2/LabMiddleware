using Middleware.Core.Parsers;
using Middleware.Core.Builders;

namespace Middleware.Core.Services
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
