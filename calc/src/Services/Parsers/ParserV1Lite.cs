using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUWM.Servers.Core.Calc.Models;

namespace NUWM.Servers.Core.Calc.Services.Parsers
{
    internal class ParserV1Lite : IBaseItemFileParser
    {
        private static readonly Regex _regex =
            new Regex(@"((?m)^\d+[^\s]\d*)\s*(\W*)\s(дані відсутні|\d*[,]\d*)", RegexOptions.ECMAScript);

        public ParserV1Lite(int year, string path, bool isAlternate = false)
        {
            Year = year;
            Path = path;
            IsAlternate = isAlternate;
        }

        public int Year { get; }
        public string Path { get; }
        public bool IsAlternate { get; }

        public IEnumerable<BaseItem> ParseFile()
        {
            using (var f = File.OpenText(Path))
            {
                while (!f.EndOfStream)
                {
                    var l = f.ReadLine();
                    if (l != null)
                    {
                        if (l.StartsWith('#')) continue;
                        if (string.IsNullOrEmpty(l)) continue;
                        var m = _regex.Match(l);
                        List<string> vals = new List<string>();
                        foreach (Group t in m.Groups)
                            vals.Add(t.Value);
                        if (vals[3].ToLower().Contains("дані відсутні"))
                        {
                            vals[3] = "0";
                        }
                        var b = new BaseItem
                        {
                            Code = vals[1],
                            Title = vals[2],
                            PassMarks = new Dictionary<int, double> { { Year, double.Parse(vals[3].Replace(',', '.')) } }
                        };
                        if (b.Code.Length > 3)
                        {
                            b.Code = b.Code.Trim('0');
                        }
                        else if (b.Code.Length < 3)
                        {
                            b.Code = '0' + b.Code;
                        }
                        yield return b;
                    }

                }
            }
        }
    }
}