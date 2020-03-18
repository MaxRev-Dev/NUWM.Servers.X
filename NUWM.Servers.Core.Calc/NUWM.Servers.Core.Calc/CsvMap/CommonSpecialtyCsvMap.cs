using System.Collections.Generic;
using CsvHelper.Configuration;
using NUWM.Servers.Core.Calc.Models;

namespace NUWM.Servers.Core.Calc.CsvMap
{
    public sealed class CommonSpecialtyCsvMap : ClassMap<BaseItem>
    {
        public CommonSpecialtyCsvMap(int year)
        {
            Map(x => x.Code)
                .ConvertUsing(x =>
                    SpecialtyCodeNormalizer.Normalize(x.GetField(0)));
            Map(x => x.Title)
                .ConvertUsing(x => x.GetField(1));
            Map(x => x.SubTitle)
                .ConvertUsing(x => x.GetField(2));
            Map(x => x.Modulus.CoefName)
                .ConvertUsing(x => new[]
                {
                    x.GetField(3).Trim(),
                    x.GetField(4).Trim(),
                    x.GetField(5).Trim(),
                });

            double basicNumericParser(string value) => double.Parse(value.Replace(',', '.'));
            double advNumericParser(string value) => double.Parse(value.Replace(',', '.').Split(new[] { ' ' })[0]);

            Map(x => x.Modulus.Coef)
                .ConvertUsing(x => new[]
                {
                    basicNumericParser(x.GetField(6)),
                    basicNumericParser(x.GetField(7)),
                    basicNumericParser(x.GetField(8)),
                });
            Map(x => x.PassMarks)
                .ConvertUsing(x =>
                {
                    var u = x.GetField("Прохідний бал");
                    return new Dictionary<int, double> {{year, double.Parse(u)}};
                });
            Map(x => x.BranchCoef)
                .ConvertUsing(x => advNumericParser(x.GetField("ГК")));
            Map(x => x.IsSpecial)
                .ConvertUsing(x => x.GetField("SP") == "#");
        }

    }
}
