using Middleware.Core.Models;
using System.Collections.Generic;

namespace Middleware.Core.Parsers
{
    public interface IAnalyzerParser
    {
        /// <summary>
        /// Convierte el mensaje crudo de la máquina en un listado de resultados de laboratorio
        /// </summary>
        /// <param name="rawMessage">Mensaje recibido del analizador</param>
        /// <returns>Lista de LabResult</returns>
        List<LabResult> Parse(string rawMessage);
    }
}
