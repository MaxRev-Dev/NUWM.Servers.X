using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NUWM.Servers.Core.Calc
{
    public class ModulusEncounter
    {
        public List<ModulusList> ModulusList { get; } = new List<ModulusList>();
        public async void Reader(StreamReader reader)
        {
            string all = await reader.ReadToEndAsync();

            string[] part = all.Split("Код");
            for (int i = 1; i < part.Count(); i++)
            {
                ParsePart(string.Format("Код\n" + part[i]));
            }
            reader.Close();
            reader.Dispose();
        }

        private void ParsePart(string part)
        {
            string f1 = @"(?<=Код)((?s).*?)(?=Для участі)",
                f2 = @"(?=Для участі)((?s).*?)(?=Вагові)",
                f3 = @"(?=Вагові)((?s).*?)(?=\s\s\s|Для)";

            string
                p_code = @"\n*([0-9].*)\n*(\W*)",
                //takepart = @"(Для участі.*\W*)",
                sptitle = @"(?=Спеціальність)\W*?(?=\).*|W[^\W]).",
                coefs_names = @"(?<=\d[.])\W[^\n]*",
                coefs = @"0,\d*";

            var namesAndCoefsMatch = new Regex(f1).Matches(part);
            var budgetCnamesAndCoefs = new Regex(f2).Matches(part);
            var contractCnamesAndCoefs = new Regex(f3).Matches(part);

            var namesAndCoefs = namesAndCoefsMatch[0].Value;
            var tc = new Regex(sptitle, RegexOptions.ECMAScript).Match(namesAndCoefs);
            if (tc.Groups.Count > 0)
            {
                namesAndCoefs = namesAndCoefs.Replace(tc.Value, "");
            }
            var nameslists = new List<string>();
            foreach (Match m in new Regex(p_code, RegexOptions.ECMAScript).Matches(namesAndCoefs))
            {
                nameslists.Add(m.Value);
            }
            var NAndC = new List<Tuple<string, string>>();
            foreach (var i in nameslists)
            {
                var pp = new Regex(p_code, RegexOptions.ECMAScript).Match(i);
                NAndC.Add(new Tuple<string, string>(pp.Groups[1].Value, Regex.Unescape(pp.Groups[2].Value.Normalize()).TrimEnd(' ').TrimStart(' ')));
            }

            var cnameslists = new List<string>[2];
            var ccoefslists = new List<string>[2];
            int cnt = 0;
            foreach (Match m in budgetCnamesAndCoefs)
            {
                cnameslists[cnt] = new List<string>();
                foreach (Match u in new Regex(coefs_names, RegexOptions.ECMAScript).Matches(m.Groups[0].Value))
                {

                    cnameslists[cnt].Add(u.Value);
                }
                cnt++;
            }
            cnt = 0;
            foreach (Match m in contractCnamesAndCoefs)
            {
                ccoefslists[cnt] = new List<string>();
                var fullname = new Regex(@"(?=Вагові)\W*[.]", RegexOptions.ECMAScript).Matches(m.Groups[0].Value)[0].Value;

                foreach (Match u in new Regex(coefs).Matches(m.Groups[0].Value.Replace(fullname, "")))
                {
                    ccoefslists[cnt].Add(u.Value);
                }
                cnt++;
            }
            int io = 0;
            var d = new List<double>();
            var dd = new List<double>();
            while (true)
            {
                ccoefslists[0][io] = ccoefslists[0][io].Replace(',', '.');
                d.Add(double.Parse(ccoefslists[0][io]));
                ccoefslists[1][io] = ccoefslists[1][io].Replace(',', '.');
                dd.Add(double.Parse(ccoefslists[1][io]));
                io++;
                if (io == ccoefslists[0].Count) break;
            }
            var hx = new List<List<double>>();
            var hx2 = new List<List<double>>();

            var nhx = new List<List<string>>();
            var nhx2 = new List<List<string>>();
            while (true)
            {
                nhx.Add(cnameslists[0].Take(3).ToList()); cnameslists[0].RemoveRange(0, 3);
                nhx2.Add(cnameslists[1].Take(3).ToList()); cnameslists[1].RemoveRange(0, 3);
                hx.Add(d.Take(3).ToList());
                d.RemoveRange(0, 3);
                hx2.Add(dd.Take(3).ToList());
                dd.RemoveRange(0, 3);
                if (dd.Count == 0) break;
            }
            int p = 0;
            foreach (var t in NAndC)
            {
                ModulusList.Add(new ModulusList
                {
                    Name = t.Item2.Replace('\n', ' ').Replace("  ", " ").Replace('’', '\'').Replace('`', '\'').Trim(),
                    Coef = hx[p].ToArray(),
                    CoefName = nhx[p].ToArray()
                }); p++;
            }
        }
    }
}