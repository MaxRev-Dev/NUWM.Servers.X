using System.Collections.Generic;
using NUWM.Servers.Core.Calc.Models;

namespace NUWM.Servers.Core.Calc.Services.Parsers
{
    public interface IBaseItemFileParser
    {
        int Year { get; }
        string Path { get; }
        bool IsAlternate { get; }
        IEnumerable<BaseItem> ParseFile();
    }
}