using System.Collections.Generic;
using CsvHelper.Configuration;

namespace NUWM.Servers.Core.Calc
{
    public sealed class CommonSpecialtyCsvMap : ClassMap<BaseItem>
    {
        public CommonSpecialtyCsvMap()
        {
            Map(x => x.Code)
                .ConvertUsing(x =>
                {
                    var bs = x.GetField(0);
                    if (bs.Length > 3)
                    {
                        bs = bs.Trim('0');
                    }
                    else if (bs.Length < 3)
                    {
                        bs = '0' + bs;
                    }
                    return bs;
                });
            Map(x => x.Title)
                .ConvertUsing(x => x.GetField(1));
            Map(x => x.SubTitle)
                .ConvertUsing(x => x.GetField(2));
            Map(x => x.Modulus.CoefName)
                .ConvertUsing(x => {
                    var x1 = x.GetField(3).Trim();
                    return new[]
                        {
                       x1,
                        x.GetField(4).Trim(),
                        x.GetField(5).Trim(),
                        };
                        });
            Map(x => x.Modulus.Coef)
                .ConvertUsing(x =>
                   new[]
                   {
                         double.Parse(x.GetField(6).Replace(',','.')),
                         double.Parse(x.GetField(7).Replace(',','.')),
                         double.Parse(x.GetField(8).Replace(',','.'))
                   });
            Map(x => x.PassMarks)
                .ConvertUsing(x => new Dictionary<int, double> { { 2018, double.Parse(x.GetField(9)) } });
            Map(x => x.BranchCoef)
                .ConvertUsing(x => double.Parse(x.GetField("ГК").Replace(',', '.').Split(new[] { ' ' })[0]));
            Map(x => x.IsSpecial)
                .ConvertUsing(x => x.GetField("SP") == "#");
        }
    }
}
