using System;
using System.Collections.Generic;
using System.Linq;
using MaxRev.Utils;
using NUWM.Servers.Core.Calc.Config;
using NUWM.Servers.Core.Calc.Models;
using NUWM.Servers.Core.Calc.Services.Parsers;

namespace NUWM.Servers.Core.Calc.Services
{
    public class Calculator
    {
        public Calculator(CalcConfig config)
        {
            _config = config;
        }

        public Calculator OnError(Action<Exception> action)
        {
            _onError = action ?? throw new ArgumentNullException(nameof(action));
            return this;
        }
        private bool Contains(string[] arr1, string[] arr2, bool force = false)
        {
            try
            {
                var local = new List<int>();
                foreach (var t in arr2)
                {
                    bool found = false;
                    for (int i = 0; i < arr1.Length; i++)
                    {
                        if (!arr1[i].Contains(t, StringComparison.InvariantCultureIgnoreCase) || local.Contains(i))
                            continue;
                        local.Add(i);
                        found = true;
                        break;
                    }
                    if (!found) return false;
                }
            }
            catch (Exception ex)
            {
                NotifyLoggerError(new Exception(string.Join(",", arr1) + "\n\n" + string.Join(",", arr2), ex));
                if (force) return false;
                throw;
            }
            return true;
        }

        private void NotifyLoggerError(Exception exception)
        {
            _onError?.Invoke(exception);
        }

        private Action<Exception> _onError;
        private readonly CalcConfig _config;

        public Tuple<List<CalculatedSpecialty>, CalcMarkInfo> Calculate(
            SpecialtyParser parser,
            string[] coefnames,
            IReadOnlyList<int> userMarks,
            double averageMark,
            int prior, bool village,
            double? prepCourses,
            int? ukrOlimp)
        {
            var year = TimeChron.GetRealTime().Year - 1;
            var obj = new List<CalculatedSpecialty>();
            IEnumerable<SpecialtyInfo> listing;

            if (prepCourses.HasValue)
            {
                // alternate coefs
                listing = parser.AlternateList.ToArray();
            }
            else
            {
                listing = parser.SpecialtyList.ToArray();
            }

            for (int l = 0; l < coefnames.Length; l++)
            {
                try
                {
                    listing = listing.Where(x => x.Modulus.CoefName[0] != default && Contains(x.Modulus.CoefName, coefnames)).ToArray();
                }
                catch
                {
                    string all = "";
                    foreach (var i in listing.Where(x => x.Modulus.CoefName.Any(t => t == null)))
                    {
                        all += i.Title + '\n' + i.SubTitle + "\n\n";
                    }
                    NotifyLoggerError(new Exception(all));
                    listing = listing.Where(x => Contains(x.Modulus.CoefName, coefnames, true)).ToArray();
                }
            }

            double min = 200, max = 0;
            foreach (var i in listing)
            {
                if (!i.PassMarks.ContainsKey(year)) continue;
                var dictionary = new Dictionary<string, double>();
                var spNames = i.Modulus.CoefName.ToList();
                var spCoefs = i.Modulus.Coef.ToList();

                for (int l = 0; l < spNames.Count; l++)
                    dictionary.Add(spNames[l], spCoefs[l]);
                double accum = 0;
                string path = "(";

                // zno marks
                for (int index = 0; index < dictionary.Count; index++)
                {
                    string current = dictionary.Keys.First(cx => cx.ToLower().Contains(spNames[index].ToLower()));
                    if (ukrOlimp.HasValue && i.IsSpecial)
                    {
                        var ukrOlimpMark = _config.UkrOlimpMark;
                        var indexer = new double[dictionary.Count];
                        indexer[ukrOlimp.Value] = ukrOlimpMark;
                        accum += dictionary[current] * (userMarks[index] + indexer[index]);
                        path += $"{dictionary[current]} * " +
                               (ukrOlimp == index ?
                                   $"({userMarks[index]} + {indexer[index]})" :
                                   $"{userMarks[index]}") + " + ";
                    }
                    else
                    {
                        accum += dictionary[current] * userMarks[index];
                        path += $"{dictionary[current]} * {userMarks[index]} + ";
                    }
                }

                // find nearest gradue mark 
                var txg = parser.ConverterTable.Keys.Where(v => Math.Abs(Math.Round(v, 1) - averageMark) < 0.00001);
                var enumerable = txg as double[] ?? txg.ToArray();
                if (enumerable.Length == 0)
                {
                    return new Tuple<List<CalculatedSpecialty>, CalcMarkInfo>(obj, default);
                }
                var tg = enumerable.First();
                accum += 0.1 * parser.ConverterTable[tg]; // at. aver mark
                path += $"0.1 * {parser.ConverterTable[tg]}";

                // prep courses of NUWM
                if (prepCourses.HasValue && i.IsSpecial)
                {
                    accum += 0.05 * prepCourses.Value;
                    path = path.TrimEnd() + $" + 0.05 * {prepCourses}";
                }

                accum *= 1.04; // regional coefs
                path += ") * 1.04 ";

                if (prior == 1 || prior == 2)
                {
                    accum *= i.BranchCoef;// 1.02;
                    path += $"* {i.BranchCoef} ";
                }

                if (village) // village
                {
                    if (i.IsSpecial)
                    {
                        accum *= 1.05;
                        path += "* 1.05 ";
                    }
                    else
                    {
                        accum *= 1.02;
                        path += "* 1.02 ";
                    }
                }

                if (accum > 200) accum = 200;
                if (accum > max) max = accum;
                if (accum < min) min = accum;
                obj.Add(new CalculatedSpecialty(i) { YourAverMark = Math.Round(accum, 1), PassMark = i.PassMarks[year], CalcPath = path.Trim() });
            }
            obj.Sort((y, x) => x.YourAverMark.CompareTo(y.YourAverMark));
            return new Tuple<List<CalculatedSpecialty>, CalcMarkInfo>(obj,
                new CalcMarkInfo
                {
                    Aver = Math.Round((min + max) * 1.0 / 2, 2),
                    Min = Math.Round(min, 2),
                    Max = Math.Round(max, 2)
                });
        }
    }
}