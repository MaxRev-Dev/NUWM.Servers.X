using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using NUWM.Servers.Core.Calc.CsvMap;
using NUWM.Servers.Core.Calc.Models;

namespace NUWM.Servers.Core.Calc.Services.Parsers
{
    internal class ParserV2Full : IBaseItemFileParser
    {
        private readonly CsvConfiguration _csvConfiguration =
            new CsvConfiguration(new CultureInfo("uk-UA"))
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

        public ParserV2Full(int year, string path, bool isAlternate = false)
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
            using (var fs = File.Open(Path, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            using (var r = new CsvReader(sr, _csvConfiguration))
            {
                var map = new CommonSpecialtyCsvMap(Year);
                r.Configuration.RegisterClassMap(map);
                foreach (SpecialtyInfo specialtyInfo in r.GetRecords<SpecialtyInfo>())
                    yield return specialtyInfo;
            }
        }

    }
}