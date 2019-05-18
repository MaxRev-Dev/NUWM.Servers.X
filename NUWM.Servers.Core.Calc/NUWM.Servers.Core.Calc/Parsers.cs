using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CsvHelper;
using CsvHelper.Configuration;
using HtmlAgilityPack;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using MaxRev.Utils.Methods;

namespace NUWM.Servers.Core.Calc
{
    public enum KeyFile
    {
        Table2018,
        TableV1,
        SpecV1,
        PassV1,
        RemoveSpecs
    }
    public class SpecialtyParser
    {
        private Dictionary<KeyFile, string> FKeys { get; }
        public string
            abitUrl = "http://start.nuwm.edu.ua",
            catalogueUrl = "/perelik";
        private List<string> _removeFromTable;
        public string HasError { get; internal set; }
        public List<SpecialtyInfo> SpecialtyList { get; private set; } = new List<SpecialtyInfo>();
        public Dictionary<double, int> ConverterTable { get; private set; } = new Dictionary<double, int>();
        public SpecialtyParser()
        {
            FKeys = new Dictionary<KeyFile, string>
            {
                { KeyFile.SpecV1, Path.Combine(App.Get.Core.DirectoryManager[App.Dirs.AddonsCalc], "SpSpec.txt")},
                { KeyFile.PassV1,Path.Combine(App.Get.Core.DirectoryManager[App.Dirs.AddonsCalc], "passMark.txt")},
                { KeyFile.RemoveSpecs,Path.Combine(App.Get.Core.DirectoryManager[App.Dirs.AddonsCalc], "ban.txt")},
                { KeyFile.Table2018, Path.Combine(App.Get.Core.DirectoryManager[App.Dirs.Addons], "Table2018.csv")}

            };
            LoadSpecialyRemoveList();
            RunAsync();
        }
        public async void RunAsync()
        {
            CreateTableForZNOConvert();

            await ReloadTables();
        }
        public async Task ReloadTables()
        {
            try
            {
                await GetLastModulus();

                LinkSpecialItemsV1();
            }
            catch (Exception ex)
            {
                App.Get.Core.Logger.NotifyError(LogArea.Other, ex);
            }
        }
        public IEnumerable<SpecialtyInfo> LoadTableV2()
        {
            var file = FKeys[KeyFile.Table2018];
            if (File.Exists(file))
            {
                using (var fs = File.Open(file, FileMode.Open, FileAccess.Read))
                using (var sr = new StreamReader(fs))
                using (var r = new CsvReader(sr, new Configuration(new CultureInfo("uk-UA"))
                {
                    HasHeaderRecord = true,
                    Delimiter = ","
                }))
                {
                    r.Configuration.RegisterClassMap<CommonSpecialtyCsvMap>();
                    return r.GetRecords<SpecialtyInfo>().ToList();
                }
            }
            return Array.Empty<SpecialtyInfo>();
        }
        private void LoadSpecialyRemoveList()
        {
            string file = FKeys[KeyFile.RemoveSpecs];
            if (File.Exists(file))
            {
                _removeFromTable = new List<string>();
                using (var t = File.OpenText(file))
                {
                    while (!t.EndOfStream)
                    {
                        var h = t.ReadLine();
                        if (h != null)
                        {
                            if (h.StartsWith("#")) continue;
                            _removeFromTable.Add(h.Replace('\n', '\n'));
                        }

                    }
                }
            }
        }

        public void CreateTableForZNOConvert()
        {
            var list = new Dictionary<double, int>();
            int mark = 100;
            for (double i = 0; i <= 12; i += 0.1)
            {
                if (i < 2) list.Add(i, 100);
                else list.Add(i, mark++);
            }

            ConverterTable = list;
        }
        public void LinkSpecialItemsV1()
        {
            int year = 2017;
            var list = new List<BaseItem>();
            var list2 = new List<BaseItem>();
            try
            {
                string path = FKeys[KeyFile.SpecV1];//
                if (File.Exists(path))
                {
                    using (var file = File.OpenText(path))
                    {
                        var r = new Regex(@"(\d\d)\s*(\W*)\s(\d*.\d*)\s*(\W*)", RegexOptions.ECMAScript);
                        while (!file.EndOfStream)
                        {
                            var l = file.ReadLine();
                            if (l != null)
                            {
                                if (l.StartsWith('#')) continue;
                                var m = r.Match(l);
                                var bs = new BaseItem
                                {
                                    IsSpecial = l.StartsWith('*'),
                                    InnerCode = m.Groups[1].Value.Replace('\t', ' ').Replace('*', ' ').TrimStart(' ').TrimEnd(' '),
                                    Branch = m.Groups[2].Value.TrimStart(' ').Replace('\t', ' ').TrimEnd(' '),
                                    Code = m.Groups[3].Value.Replace('\t', ' ').TrimStart(' ').TrimEnd(' '),
                                    Title = m.Groups[4].Value.Replace('\t', ' ').TrimStart(' ').TrimEnd(' ')
                                };
                                if (bs.Code.Length > 3)
                                {
                                    bs.Code = bs.Code.Trim('0');
                                }
                                else if (bs.Code.Length < 3)
                                {
                                    bs.Code = '0' + bs.Code;
                                }
                                list.Add(bs);
                            }
                        }
                    }
                }

                path = FKeys[KeyFile.PassV1];
                if (File.Exists(path))
                {
                    string mix = @"((?m)^\d+[^\s]\d*)\s*(\W*)\s(дані відсутні|\d*[,]\d*)";
                    using (var f = File.OpenText(path))
                    {
                        var r = new Regex(mix, RegexOptions.ECMAScript);
                        while (!f.EndOfStream)
                        {
                            var l = f.ReadLine();
                            if (l != null)
                            {
                                if (l.StartsWith('#')) continue;
                                if (string.IsNullOrEmpty(l)) continue;
                                var m = r.Match(l);
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
                                    PassMarks = new Dictionary<int, double> { { year, double.Parse(vals[3].Replace(',', '.')) } }
                                };
                                if (b.Code.Length > 3)
                                {
                                    b.Code = b.Code.Trim('0');
                                }
                                else if (b.Code.Length < 3)
                                {
                                    b.Code = '0' + b.Code;
                                }
                                list2.Add(b);
                            }

                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }

            var tmp = SpecialtyList.Cast<BaseItem>().Where(x => x != default).ToList();

            foreach (var b in tmp)
                if (b.Code.Length > 3)
                {
                    b.Code = b.Code.Trim('0');
                }
                else if (b.Code.Length < 3)
                {
                    b.Code = '0' + b.Code;
                }

            JoinList(tmp, list, x => x.IsSpecial);
            JoinList(tmp, list2, x => x.PassMarks);
            var l2 = LoadTableV2().Cast<BaseItem>().ToList();
            JoinList(tmp, l2, x => x.IsSpecial);
            JoinList(tmp, l2, x => x.BranchCoef);
            JoinList(tmp, l2, x => x.Modulus);
            foreach (var i in tmp)
                i.Modulus.Name = i.Title;

            JoinList(tmp, l2, x => x.PassMarks);
            SpecialtyList = tmp.Cast<SpecialtyInfo>().ToList();
        }
        public PropertyInfo GetPropertyInfo<TSource, TProperty>(TSource _, Expression<Func<TSource, TProperty>> propertyLambda)
        {
            var type = typeof(TSource);

            if (!(propertyLambda.Body is MemberExpression member))
                throw new ArgumentException($"Expression '{propertyLambda}' refers to a method, not a property.");

            var propInfo = member.Member as PropertyInfo;
            if (propInfo == null)
                throw new ArgumentException($"Expression '{propertyLambda}' refers to a field, not a property.");

            if (type != propInfo.ReflectedType &&
                !type.IsAssignableFrom(propInfo.ReflectedType))
                throw new ArgumentException(
                    $"Expression '{propertyLambda}' refers to a property that is not from type {type}.");

            return propInfo;
        }
        private void JoinList<T, Prop>(List<T> main, List<T> app, params Expression<Func<T, Prop>>[] selectors)
            where T : ICodeItem

        {
            var func = selectors.Select((x, i) => new { i, f = x.Compile() }).ToArray();
            foreach (var i in main)
            {
                foreach (var j in app)
                {
                    if (i.Code == j.Code)
                    {
                        int it = 0;
                        foreach (var selector in selectors)
                        {
                            var fv = func.ElementAt(it).f(j);
                            var p = GetPropertyInfo(i, selector);
                            var f = p.PropertyType.GetInterfaces();
                            if (f.Any(x => x.IsGenericType &&
                            x.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                            {
                                var d = (IDictionary<int, double>)p.GetValue(i);
                                var d2 = (IDictionary<int, double>)fv;
                                foreach (var di in d2)
                                    if (!d.ContainsKey(di.Key))
                                        d.Add(di);
                            }
                            //else if (p.PropertyType == typeof(bool))
                            //{
                            //    var v1 = p.GetValue(i);
                            //    p.SetValue(i, (bool)Convert.ChangeType(fv, typeof(bool)) || ((bool)v1));
                            //}
                            else
                                p.SetValue(i, fv);
                            it++;
                        }
                    }
                }
            }
        }

        internal void LoadSpecialtyList(IEnumerable<SpecialtyInfo> list)
        {
            SpecialtyList = list.ToList();
        }

        public async Task GetLastModulus()
        {
            RequestAllocator.Instance.MaxAsyncRequests = 3;
            var watch = Stopwatch.StartNew();
            SpecialtyList = new List<SpecialtyInfo>();
            var requestUri = new Uri(abitUrl + catalogueUrl);
            try
            {
                var doc = new HtmlDocument();
                using (var r = new Request(requestUri.AbsoluteUri))
                {
                    var s = await r.GetAsync();
                    s.EnsureSuccessStatusCode();
                    doc.LoadHtml(await s.Content.ReadAsStringAsync());
                }
                var mc = doc.DocumentNode.Descendants().Where(x => x.HasClass("wk-accordion")).First();
                var des = mc.Descendants("tr").Where(trd => trd.FirstChild.InnerText != "Код").ToArray();

                // var currentCount = des.Count();
                int item = 0;
                var tasks = new List<Task>();
                foreach (var el in des)
                {
                    string f = HttpUtility.HtmlDecode(el.Elements("td").First().InnerText);
                    var t = string.Join("", (f ?? throw new InvalidOperationException()).Where(char.IsDigit));
                    var code = int.Parse(t);
                    var content = el.Elements("td").Last();
                    var itemIndex = item++;
                    //  var thisurl = el.Elements("td").Last().Element("a").GetAttributeValue("href", "null");
                    //if (_removeFromTable != null && _removeFromTable.Contains(
                    //    abitUrl + thisurl))
                    //{
                    //    currentCount--; continue;
                    //}
                    //if (thisurl.Contains("wiki.nuwm"))
                    //{
                    //    currentCount--; continue;
                    //}
                    tasks.Add(GetPage(code, content, itemIndex));
                }

                await Task.WhenAll(tasks.ToArray());
                watch.Stop();
#if DEBUG
                var elapsedMs = watch.ElapsedMilliseconds;
                var g = TimeSpan.FromMilliseconds(elapsedMs);
                Console.WriteLine($"Parse time: {g.TotalSeconds} s");
#endif
            }
            catch (Exception ex)
            {
                App.Get.Core.Logger.NotifyError(LogArea.Other, ex);
                await App.Get.LoadCache();
            }
        }

        private async Task GetPage(int code, HtmlNode el, int item)
        {
            var linker = el.Element("a").GetAttributeValue("href", "null");

            if (linker != "null")
            {
                if (linker.StartsWith("htt"))
                    linker = linker.Replace(abitUrl, "");
                var link = abitUrl + linker;
                using (var request = new Request(link))
                {
                    try
                    {
                        await RequestAllocator.RetryFor(request, rm =>
                         {
                             rm.EnsureSuccessStatusCode();
                             ParsePage(rm.Content, linker);
                         }, 10);
                    }
                    catch (Exception ex)
                    {
                        App.Get.Core.Logger.NotifyError(LogArea.Other, new Exception(
                           string.Join('\n', new string('-', 20), code, HtmlEntity.DeEntitize(el.InnerText).Trim(' '), link,
                               $"Item {item} fetch error: " + ex.Message, new string('-', 20))));
                    }
                }
            }
        }

        private async void ParsePage(HttpContent httpContent, string link)
        {
#if DEBUG
            Console.WriteLine($"Parsing {link}");
#endif
            var page = await httpContent.ReadAsStringAsync();
            httpContent.Dispose();
            var doc = new HtmlDocument();
            doc.LoadHtml(page);
            var sp = new SpecialtyInfo
            {
                Content = new ContentVisualiser { Content = new Dictionary<string, List<string>>() },
                Links = new LinksVisualiser { Links = new Dictionary<string, List<LinkItem>>() },
                ChairsProvidesProg = new TupleVisualiser()
            };

            try
            {
                var f = doc.GetElementbyId("yoo-zoo").Element("div");
                foreach (var el in f.Elements("div"))
                {
                    switch (el.GetAttributeValue("class", "null"))
                    {
                        case "pos-top":
                            {
                                // List<string> tmpContent = new List<string>();
                                var er = el.Element("div");
                                sp.BranchName = new Item
                                {
                                    Content = new List<string>()
                                };
                                sp.BranchName.Content.Add(er.ChildNodes[2].InnerText);
                                var urltmp = er.ChildNodes[2].GetAttributeValue("href", "");
                                sp.BranchName.Url = string.IsNullOrEmpty(urltmp) ? "" : abitUrl + urltmp;
                                sp.BranchName.Title = er.ChildNodes[1].InnerText.TrimEnd(' ').TrimStart(' ');
                                break;
                            }
                        case "pos-content":
                            {
                                foreach (var i in el.Elements("div"))
                                {
                                    var atr = i.GetAttributeValue("class", "null");
                                    if (atr != "null")
                                    {
                                        switch (atr.Substring("element element-".Length))
                                        {
                                            /* case "relateditems":
                                                 {
                                                     try
                                                     {
                                                         if (i.Element("h3").InnerText.ToLowerInvariant().Contains("контракт"))
                                                         {
                                                             sp.Modulus = Specialty.ModulusList.GetModulusFromHtml(i.Element("ul").Elements("li"), sp.Modulus, false);
                                                         }
                                                         else if (i.Element("h3").InnerText.ToLowerInvariant().Contains("держ"))
                                                         {
                                                             sp.Modulus = Specialty.ModulusList.GetModulusFromHtml(i.Element("ul").Elements("li"), sp.Modulus, true);
                                                         }
                                                     }
                                                     catch (Exception)
                                                     {
                                                         Console.SetCursorPosition(0, rx++);
                                                         Console.Write("Check this url: " + sitesUrl + link as string);
                                                     }
                                                     break;
                                                 }*/
                                            case "text first":
                                                {
                                                    try
                                                    {
                                                        sp.Code = i.ChildNodes[2].InnerText;
                                                    }
                                                    catch (Exception)
                                                    {

#if DEBUG
                                                        Console.WriteLine("Check this url: " + abitUrl + link);
#endif
                                                    }
                                                    break;
                                                }
                                            case "text":
                                                {
                                                    List<string> tmpContent = new List<string>
                                                        {
                                                            i.ChildNodes[2].InnerText
                                                        };
                                                    sp.Content.Content.Add(i.ChildNodes[1].InnerText, tmpContent);
                                                    break;
                                                }
                                            case "textarea":
                                                {
                                                    List<string> list = new List<string>();
                                                    var ds = i.Element("h3").NextSibling;
                                                    list.Add(ds.OuterHtml);
                                                    while (true)
                                                    {
                                                        if (ds.NextSibling != null)
                                                        {
                                                            ds = ds.NextSibling;
                                                            if (ds.OuterHtml.Length > 3)
                                                                list.Add(ds.OuterHtml);
                                                        }
                                                        else break;
                                                    }
                                                    sp.Content.Content.Add(i.ChildNodes[1].InnerText, list);
                                                    break;
                                                }
                                            case "link":
                                                {

                                                    var links = new List<LinkItem>();
                                                    foreach (var op in i.Elements("a"))
                                                    {
                                                        links.Add(new LinkItem
                                                        {
                                                            Url = op.GetAttributeValue("href", ""),
                                                            Title = op.GetAttributeValue("title", "")
                                                        });
                                                    }

                                                    sp.Links.Links.Add(i.ChildNodes[1].InnerText, links);

                                                    break;
                                                }
                                            case "relatedcategories":
                                                {
                                                    var links = new List<LinkItem>();
                                                    foreach (var r in i.ChildNodes[2].ChildNodes)
                                                    {
                                                        var urltmp = r.ChildNodes[0].GetAttributeValue("href", "");
                                                        links.Add(new LinkItem
                                                        {
                                                            Url = string.IsNullOrEmpty(urltmp) ? "" : abitUrl + urltmp,
                                                            Title = r.ChildNodes[0].InnerText
                                                        });
                                                    }
                                                    sp.ChairsProvidesProg.ChairsProvidesProg = new Tuple<string, List<LinkItem>>(i.ChildNodes[1].InnerText, links);
                                                    break;
                                                }
                                        }
                                    }
                                }
                                break;
                            }
                    }
                }
                var title = f.Element("h1").InnerText.Trim();
                if (title.Contains('('))
                {
                    sp.Title = title.Substring(0, title.IndexOf('('));
                    var sb = new Regex(@"(?<=\()\W.*?(?=\))", RegexOptions.ECMAScript).Match(title).Groups[0].Value;
                    if (sb != "Бакалавр")
                    {
                        sp.SubTitle = UppercaseFirst(sb);
                    }
                }
                else
                {
                    sp.Title = title;
                }
                sp.Title = sp.Title.Trim().Replace('\n', ' ').Replace("  ", " ").Replace('’', '\'').Replace('`', '\'');
                sp.URL = abitUrl + link;

                SpecialtyList.Add(sp);
            }
            catch (Exception ex)
            {
                sp.Errors = new List<string>
                    {
                        ex.Message+ ' '+ Tools.AnonymizeStack(ex.StackTrace)
                    };
                SpecialtyList.Add(sp);
            }

#if DEBUG
            Console.WriteLine($"Finished {link}");
#endif
        }
        public List<string> GetUnique()
        {
            if (App.Get.SpecialtyParser.SpecialtyList.Count == 0) return new List<string>();
            List<string> dist = new List<string>();
            foreach (var t in App.Get.SpecialtyParser.SpecialtyList)
            {
                dist.AddRange(t.Modulus.CoefName);
            }
            var all = dist.Distinct().Where(x => !string.IsNullOrEmpty(x)).ToArray();

            for (int t = 0; t < all.Length; t++)
            {
                all[t] = all[t].Trim(' ', '.', '1', '2', '3');
            }
            List<string> newer = new List<string>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Contains("або"))
                {
                    string[] n = all[i].Split("або", StringSplitOptions.RemoveEmptyEntries);
                    all[i] = "";
                    foreach (var t in n)
                        newer.Add(t);
                }
            }
            newer.AddRange(all);
            for (int t = 0; t < newer.Count; t++)
            {
                newer[t] = newer[t].TrimStart(' ').TrimEnd(' ');
                newer[t] = UppercaseFirst(newer[t]);
            }
            return newer.Where(x => !string.IsNullOrEmpty(x)).Distinct().Reverse().ToList();
        }

        private bool EqualsAbs(string s1, string s2, bool reverse = false)
        {
            return s1.Contains(s2) ||
             s1.Replace(" і ", " та ").Contains(s2) ||
             s1.Replace('’', '\'').Contains(s2) ||
             s1.Replace('`', '\'').Contains(s2) ||
             !reverse && EqualsAbs(s2, s1, true);
        }

        private static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}

//private SpecialtyInfo LinqEqual(SpecialtyInfo sp)
//{ 
//    ModulusEncounter me = default;
//    if (me == default)
//        return sp;
//    var nb = me.ModulusList.Where(x => EqualsAbs(x.Name, sp.Title)).ToArray();
//    if (nb.Length > 0)
//    {
//        sp.Modulus = nb.First();
//    }
//    else if (nb.Length == 0)
//    {
//        //nb = me.ModulusList.Where(x =>
//        //x.Code.TrimEnd(' ').TrimStart(' ').Replace('\n', ' ').Replace("  ", " ")
//        //     .Contains(sp.Code.TrimEnd(' ').TrimStart(' ').Replace('\n', ' ').Replace("  ", " ")));
//        //if (nb.Count() > 0)
//        //{
//        //    sp.Modulus = nb.First();
//        //}
//        //else
//        //{
//        //    Thread.Sleep(5000);
//        //    if (lost > 10) return sp;
//        //    return LinqEqual(sp, ++lost);
//        //}
//        Console.WriteLine("Specialty: " + sp.Title + " not linked with modulus coefs");
//    }
//    return sp;
//}