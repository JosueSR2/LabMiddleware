using Middleware.Core.Models;
using System.Collections.Generic;


namespace Middleware.Core.Parsers
{
    public interface IAnalyzerParser
    {
        List<LabResult> Parse(string rawMessage);
    }

}
