using Middleware_Core.Models;
using System.Collections.Generic;


namespace Middleware_Core.Parsers
{
    public interface IAnalyzerParser
    {
        List<LabResult> Parse(string rawMessage);
    }

}
