using HelperUtilties;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using static HelperUtilties.BranchSpecialLinqer;


namespace JSON
{
    public partial class SpecialtiesVisualiser
    {
        public partial class Specialty
        {
            #region Vars
            public static Dictionary<double, int> converts = new Dictionary<double, int>();
            public static List<SpSpecialItem> specList = new List<SpSpecialItem>();
            public static List<SpPassItem> passList = new List<SpPassItem>();
            public static void SetListsAddons(Tuple<List<SpSpecialItem>, List<SpPassItem>> t)
            {
                specList = t.Item1;
                passList = t.Item2;
            }
            public static void CreateTableForZNOConvert()
            {
                Dictionary<double, int> list = new Dictionary<double, int>();
                int mark = 100;
                list.Add(0, 100);
                for (double i = 2; i <= 12; i += 0.1)
                {
                    list.Add(i, mark++);
                }

                converts = list;
            }
            public partial class ModulusList
            {
                public ModulusList()
                {
                    Coef = new double[3];
                    CoefName = new string[3];
                }

                public static ModulusList GetModulusFromHtml(IEnumerable<HtmlNode> nodes, ModulusList list)
                {
                    if (list != null)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            var trash = nodes.ElementAt(i).InnerText;
                            int breaker = trash.IndexOf(' ', trash.IndexOfAny(new char[] { '.', ',' }) - 3);
                            string num = trash.Substring(breaker + 1).Replace(',', '.');
                            double coef = double.Parse(num);

                            list.CoefName[i] = trash.Substring(0, breaker);
                            list.Coef[i] = coef;
                        }
                    }
                    return list;
                }
            }
            public class ModulusEncounter
            {
                public static ModulusEncounter Current;
                public List<ModulusList> ls = new List<ModulusList>();
                public List<Thread> lt = new List<Thread>();
                public volatile bool busy = true;
                public async void Reader(object obj)
                {
                    var reader = obj as StreamReader;
                    string all = await reader.ReadToEndAsync();

                    string[] part = all.Split("Код");
                    for (int i = 1; i < part.Count(); i++)
                    {
                        Thread thread;
                        (thread = new Thread(new ParameterizedThreadStart(Parse))).Start(string.Format("Код\n" + part[i]));
                        lt.Add(thread);
                    }
                }
                void Parse(object obj_part)
                {
                    string f1 = @"(?<=Код)((?s).*?)(?=Для участі)",
                        f2 = @"(?=Для участі)((?s).*?)(?=Вагові)",
                        f3 = @"(?=Вагові)((?s).*?)(?=\s\s\s|Для)";

                    string part = obj_part as string,
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
                    List<string> nameslists = new List<string>();
                    foreach (Match m in new Regex(p_code, RegexOptions.ECMAScript).Matches(namesAndCoefs))
                    {
                        nameslists.Add(m.Value);
                    }
                    List<Tuple<string, string>> NAndC = new List<Tuple<string, string>>();
                    foreach (var i in nameslists)
                    {
                        var pp = new Regex(p_code, RegexOptions.ECMAScript).Match(i);
                        NAndC.Add(new Tuple<string, string>(pp.Groups[1].Value, Regex.Unescape(pp.Groups[2].Value.Normalize()).TrimEnd(' ').TrimStart(' ')));
                    }

                    List<string>[] cnameslists = new List<string>[2];
                    List<string>[] ccoefslists = new List<string>[2];
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
                    #region fallen
                    /*
                    var match = new Regex(takepart, RegexOptions.ECMAScript).Matches(part);
                    List<string> alldist = new List<string>
                    {
                        match[0].Value,
                        match[1].Value
                    };

                    var t = part.Split(alldist.ToArray(), StringSplitOptions.None);
                    match = new Regex(sptitle, RegexOptions.ECMAScript).Matches(t[0]);
                    var firstval = part.Split(match[0].Value, StringSplitOptions.RemoveEmptyEntries);
                    var fst = firstval[1].Split(alldist.ToArray(), StringSplitOptions.RemoveEmptyEntries);
                    var y = new Regex(p_code, RegexOptions.ECMAScript).Matches(fst[0]);
                    var x = new Regex(coefs_names, RegexOptions.ECMAScript).Matches(fst[1]);
                    var s = new Regex(coefs, RegexOptions.ECMAScript).Matches(fst[1]);

                    List<Tuple<string, string>> tp = new List<Tuple<string, string>>();

                    for (int i = 0; i < (x.Count < s.Count ? x.Count : s.Count); i++)
                    {
                        if (x[i] != null && s[i] != null)
                            tp.Add(new Tuple<string, string>(x[i].Value, s[i].Value));
                    }

                    x = new Regex(coefs_names, RegexOptions.ECMAScript).Matches(fst[2]);
                    s = new Regex(coefs, RegexOptions.ECMAScript).Matches(fst[2]);

                    List<Tuple<string, string>> tp2 = new List<Tuple<string, string>>();
                    for (int i = 0; i < (x.Count < s.Count ? x.Count : s.Count); i++)
                    {
                        if (x[i] != null && s[i] != null)
                            tp2.Add(new Tuple<string, string>(x[i].Value, s[i].Value));
                    }
                    */
                    #endregion
                    int io = 0;
                    List<double> d = new List<double>();
                    List<double> dd = new List<double>();
                    while (true)
                    {
                        ccoefslists[0][io] = ccoefslists[0][io].Replace(',', '.');
                        d.Add(double.Parse(ccoefslists[0][io]));
                        ccoefslists[1][io] = ccoefslists[1][io].Replace(',', '.');
                        dd.Add(double.Parse(ccoefslists[1][io]));
                        io++;
                        if (io == ccoefslists[0].Count) break;
                    }
                    List<List<double>> hx = new List<List<double>>();
                    List<List<double>> hx2 = new List<List<double>>();

                    List<List<string>> nhx = new List<List<string>>();
                    List<List<string>> nhx2 = new List<List<string>>();
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
                        ls.Add(new ModulusList()
                        {
                            Code = t.Item1,
                            Name = t.Item2,
                            Coef = hx[p].ToArray(),
                            CoefName = nhx[p].ToArray()
                        }); p++;
                    }
                }
            }
            #endregion

            public class SpecialtyParser
            {
                public List<Specialty> res = new List<Specialty>();
                public static string
                    sitesUrl = "http://start.nuwm.edu.ua",
                    catUrl = "/perelik",
                    Errors = "";
                private List<string> ban;
                public static int
                    fst = "Cached: ".Length,
                    snd = "Connections closed: ".Length,
                    currentCount = 0;
                public SpecialtyParser()
                {
                    string f = "./addons/calc/ban.txt";
                    if (File.Exists(f))
                    {
                        ban = new List<string>();
                        var t = File.OpenText(f);
                        while (!t.EndOfStream)
                        {
                            var h = t.ReadLine();
                            if (h.StartsWith("#")) continue;
                            ban.Add(h.Replace('\n', '\n'));
                        }
                        t.Close();
                    }
                    Run();
                }

                private static void GetModulus()
                {
                    new Thread(new ParameterizedThreadStart((Specialty.ModulusEncounter.Current = new Specialty.ModulusEncounter()).Reader))
                    {
                        Priority = ThreadPriority.Highest
                    }.Start(
                         File.OpenText("./addons/calc/zno_modulus.txt"));


                    if (Specialty.ModulusEncounter.Current.lt.Count > 0)
                        while (true)
                        {
                            Thread.Sleep(100);
                            Specialty.ModulusEncounter.Current.lt = Specialty.ModulusEncounter.Current.lt.Where(t => t.IsAlive).ToList();
                            if (Specialty.ModulusEncounter.Current.lt.Count == 0) break;
                        }

                }


                public static string site_url = "http://nuwm.edu.ua";

               
                public static async void Run()
                {
                    GC.Collect(); 

                    #region Modulus
                    GetModulus();
                    #endregion

                    Specialty.CreateTableForZNOConvert();
                    BranchSpecialLinqer linqer = new BranchSpecialLinqer();

                    Specialty.SetListsAddons(linqer.Run());

                    StreamReader f = File.OpenText("./addons/config.txt");
                    string direct = await f.ReadToEndAsync();
                    string[] lines = direct.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);


                    Server.Server.taskDelayH = int.Parse(new Regex(@"(?<=delayH\:)[0-9]*").Match(direct).Groups[0].Value);
                    GC.Collect();
                }
                async public void GetLastModulus()
                {
                    var watch = Stopwatch.StartNew();
                    res = new List<Specialty>();
                    currentCount = 0;
                    HttpClient cl = new HttpClient();
                    Uri requestUri = new Uri(sitesUrl + catUrl);

                    try
                    {
                        HttpResponseMessage s = await cl.GetAsync(requestUri);
                        s.EnsureSuccessStatusCode();
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(await s.Content.ReadAsStringAsync());

                        var mc = doc.DocumentNode.Descendants().Where(x => x.HasClass("wk-accordion")).First();
                        var des = mc.Descendants("tr").Where(trd => trd.FirstChild.InnerText != "Код");

                        //HtmlNode mc = doc.GetElementbyId("yoo-zoo");
                        //var des = mc.Descendants("li");
                        currentCount = des.Count();
                        // UIUpdater.Write(UIUpdater.ConsoleLines.info2, String.Format("Found {0} specialties", currentCount));

                        int item = 0;// lastrow = Console.CursorTop;

                        foreach (var el in des)
                        {
                            var param = new object[4];
                            string f = HttpUtility.HtmlDecode(el.Elements("td").First().InnerText);
                            var t = string.Join("", f.Where(x => Char.IsDigit(x)));
                            param[0] = int.Parse(t);
                            param[1] = el.Elements("td").Last();
                            param[2] = item++;
                            param[3] = sitesUrl;
                            var thisurl = el.Elements("td").Last().Element("a").GetAttributeValue("href", "null");
                            if (ban != null && ban.Contains(
                                sitesUrl + thisurl))
                            {
                                currentCount--; continue;
                            }
                            if (thisurl.Contains("wiki.nuwm"))
                            {
                                currentCount--; continue;
                            }
                            new Thread(new ParameterizedThreadStart(GetPage)).Start(param);
                        }

                        while (res.Count != currentCount)
                        {
                            Thread.Sleep(100);
                        }
                        watch.Stop();
                        var elapsedMs = watch.ElapsedMilliseconds;
                        var g = TimeSpan.FromMilliseconds(elapsedMs);
                        //  UIUpdater.Write(UIUpdater.ConsoleLines.info3, string.Format("Parse time: {0} s", g.TotalSeconds));
                    }
                    catch (Exception ex)
                    {
                        Errors += "Error parsing start.nuwm.edu.ua :" + ex.Message;
                    }
                }
                static async void GetPage(Object l)
                {
                    var param = l as object[];
                    int code = (int)param[0];
                    HtmlNode el = (HtmlNode)param[1];
                    int item = (int)param[2];
                    string sitesUrl = (string)param[3];

                    var linker = el.Element("a").GetAttributeValue("href", "null");

                    if (linker != "null")
                    {
                        if (linker.StartsWith("htt"))
                            linker = linker.Replace(sitesUrl,"");
                        try
                        {
                            CreateClientRequest request = new CreateClientRequest(sitesUrl + linker);
                            HttpResponseMessage rm = await request.GetAsync();
                            rm.EnsureSuccessStatusCode();
                            new Thread(new ParameterizedThreadStart(ParsePage)).Start(new object[] { await rm.Content.ReadAsStringAsync(), linker });
                        }
                        catch (Exception ex)
                        {
                            Errors += string.Format("Item {0} error:", item) + ex.Message + "\n\n";
                        }
                    }
                }
                static int rx = 10;
                static void ParsePage(Object paged)
                {
                    var t = paged as object[];
                    var page = t[0];
                    var link = t[1];
                    HtmlDocument doc = new HtmlDocument();

                    doc.LoadHtml(page as string);

                    Specialty sp = new Specialty
                    {
                        Content = new JSON.ContentVisualiser() { Content = new Dictionary<string, List<string>>() },
                        Modulus = new ModulusList(),
                        Links = new JSON.LinksVisualiser() { Links = new Dictionary<string, List<JSON.LinkItem>>() },
                        ChairsProvidesProg = new JSON.TupleVisualiser()
                    };
                    var f = doc.GetElementbyId("yoo-zoo").Element("div");

                    try
                    {
                        foreach (var el in f.Elements("div"))
                        {
                            switch (el.GetAttributeValue("class", "null"))
                            {
                                case "pos-top":
                                    {
                                        // List<string> tmpContent = new List<string>();
                                        var er = el.Element("div");
                                        sp.BranchName = new JSON.Item
                                        {
                                            Content = new List<string>()
                                        };
                                        sp.BranchName.Content.Add(er.ChildNodes[2].InnerText);
                                        var urltmp = er.ChildNodes[2].GetAttributeValue("href", "");
                                        sp.BranchName.Url = String.IsNullOrEmpty(urltmp) ? "" : sitesUrl + urltmp;
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
                                                                Console.SetCursorPosition(0, rx++);
                                                                Console.Write("Check this url: " + sitesUrl + link as string);
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

                                                            List<JSON.LinkItem> links = new List<JSON.LinkItem>();
                                                            foreach (var op in i.Elements("a"))
                                                            {
                                                                links.Add(new JSON.LinkItem()
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
                                                            List<JSON.LinkItem> links = new List<JSON.LinkItem>();
                                                            foreach (var r in i.ChildNodes[2].ChildNodes)
                                                            {
                                                                var urltmp = r.ChildNodes[0].GetAttributeValue("href", "");
                                                                links.Add(new JSON.LinkItem()
                                                                {
                                                                    Url = String.IsNullOrEmpty(urltmp) ? "" : sitesUrl + urltmp,
                                                                    Title = r.ChildNodes[0].InnerText
                                                                });
                                                            }
                                                            sp.ChairsProvidesProg.ChairsProvidesProg = new Tuple<string, List<JSON.LinkItem>>(i.ChildNodes[1].InnerText, links);
                                                            break;
                                                        }
                                                    default:
                                                        break;
                                                }
                                            }
                                        }
                                        break;
                                    }
                                default: break;
                            }
                        }
                        var title = f.Element("h1").InnerText.TrimStart(' ').TrimEnd(' ');
                        if (title.Contains('('))
                        {
                            sp.Title = title.Substring(0, title.IndexOf('('));
                            var sb = new Regex(@"(?<=\()\W.*?(?=\))", RegexOptions.ECMAScript).Match(title).Groups[0].Value;
                            if (sb != "Бакалавр")
                                sp.SubTitle = UppercaseFirst(sb);
                        }
                        else
                        {
                            sp.Title = title;
                        }
                        sp.URL = sitesUrl + (link as string);
                        try
                        {
                            if (sp.Modulus != new ModulusList())
                            {
                                sp = LinqEqual(sp, 0);
                            }
                            var y = passList.Where(to => sp.Title.Contains(to.Name));
                            if (y.Count() == 1)
                            {
                                sp.AverMark = Math.Round(y.First().Mark, 1).ToString();
                            }
                            else
                            {
                                y = passList.Where(to => sp.Code == to.Code);
                                if (y.Count() == 1)
                                {
                                    sp.AverMark = Math.Round(y.First().Mark, 1).ToString();
                                }
                            }
                        }
                        catch (Exception) { }

                        Server.Server.CurrentParser.res.Add(sp);
                    }
                    catch (Exception ex)
                    {
                        sp.Errors = new List<string>
                    {
                        ex.Message,
                        ex.StackTrace,
                        ex.HelpLink
                    };
                        Server.Server.CurrentParser.res.Add(sp);
                    }
                }
                public List<string> GetUnique()
                {
                    if (Server.Server.CurrentParser.res.Count == 0) return new List<string>();
                    List<string> dist = new List<string>();
                    foreach (var t in Server.Server.CurrentParser.res)
                    {
                        dist.AddRange(t.Modulus.CoefName);
                    }
                    var all = dist.Distinct().Where(x => !string.IsNullOrEmpty(x)).ToArray();

                    for (int t = 0; t < all.Count(); t++)
                    {
                        all[t] = all[t].TrimStart(' ').TrimEnd(' ');
                    }
                    List<string> newer = new List<string>();
                    for (int i = 0; i < all.Count(); i++)
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
                    for (int t = 0; t < newer.Count(); t++)
                    {
                        newer[t] = newer[t].TrimStart(' ').TrimEnd(' ');
                        newer[t] = UppercaseFirst(newer[t]);
                    }
                    return newer.Where(x => !string.IsNullOrEmpty(x)).Distinct().Reverse().ToList();
                }
                private static Specialty LinqEqual(Specialty sp, int lost)
                {
                    var nb = ModulusEncounter.Current.ls.Where(x => x.Name.TrimEnd(' ').TrimStart(' ').Replace('\n', ' ').Replace("  ", " ")
                                .Contains(sp.Title.TrimEnd(' ').TrimStart(' ').Replace('\n', ' ').Replace("  ", " ")));
                    if (nb.Count() > 0)
                    {
                        sp.Modulus = nb.First();
                    }
                    else if (nb.Count() == 0)
                    {
                        nb = ModulusEncounter.Current.ls.Where(x => x.Code.TrimEnd(' ').TrimStart(' ').Replace('\n', ' ').Replace("  ", " ")
                    .Contains(sp.Code.TrimEnd(' ').TrimStart(' ').Replace('\n', ' ').Replace("  ", " ")));
                        if (nb.Count() > 0)
                        {
                            sp.Modulus = nb.First();
                        }
                        else
                        {
                            Thread.Sleep(5000);
                            if (lost > 10) return sp;
                            return LinqEqual(sp, ++lost);
                        }
                    }
                    return sp;
                }
                static string UppercaseFirst(string s)
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return string.Empty;
                    }
                    return char.ToUpper(s[0]) + s.Substring(1);
                }
            }

        }
    }
}
