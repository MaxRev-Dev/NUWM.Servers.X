using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils;
using MaxRev.Utils.FileSystem;
using NUWM.Servers.Core.Calc.Extensions;
using NUWM.Servers.Core.Calc.Models;

namespace NUWM.Servers.Core.Calc.Services.Parsers
{
    public enum KeyFile
    {
        SpecV1,
        RemoveSpecs,
    }

    public class SpecialtyParser
    {
        private readonly ILogger _logger;
        private readonly DirectoryManager<App.Directories> _directoryManager;
        private List<string> _removeFromTable;
        private static readonly Regex _yearReg = new Regex("\\d+");
        public string
            _abitUrl = "http://start.nuwm.edu.ua",
            _catalogueUrl = "/perelik";

        public SpecialtyParser(ILogger logger, DirectoryManager<App.Directories> directoryManager)
        {
            _logger = logger;
            _directoryManager = directoryManager;
            var _dm = directoryManager;
            PathMap = new Dictionary<KeyFile, string>
            {
                { KeyFile.RemoveSpecs,Path.Combine(_dm[App.Directories.AddonsCalc], "ban.txt")},
                { KeyFile.SpecV1, Path.Combine(_dm[App.Directories.AddonsCalc], "SpSpec.txt")},
            };
            LoadSpecialyRemoveList();
        }

        public string HasError { get; internal set; }
        private Dictionary<KeyFile, string> PathMap { get; }
        public List<SpecialtyInfo> SpecialtyList { get; private set; } = new List<SpecialtyInfo>();
        public IReadOnlyDictionary<double, int> ConverterTable { get; private set; } = new Dictionary<double, int>();
        public List<IBaseItemFileParser> ParserPool { get; set; } = new List<IBaseItemFileParser>();
        public delegate Task TaskDelegate();
        public event TaskDelegate OnCacheRequired;
        public event TaskDelegate OnParsed;

        public async void RunAsync()
        {
            CreateConverterTable();

            await ReloadTables();
        }

        public async Task ReloadTables()
        {
            try
            {
#if DEBUG
                if (OnCacheRequired != default)
                    await OnCacheRequired.Invoke();
                if (SpecialtyList != null && SpecialtyList.Count == 0)
#endif
                await FetchSpecialtyInfo();

                AutoFindTables();
                AutoLinkTables();
            }
            catch (Exception ex)
            {
                _logger.NotifyError(LogArea.Other, ex);
            }
        }

        private void LoadSpecialyRemoveList()
        {
            string file = PathMap[KeyFile.RemoveSpecs];
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

        public void CreateConverterTable()
        {
            // easy-peasy table
            var list = new Dictionary<double, int>();
            var currentMark = 100;
            for (double i = 0; i <= 12; i += 0.1)
            {
                // on any changes - just burn this line
                list.Add(i, i < 2 ? 100 : currentMark++);
            }
            // replace current singleton
            ConverterTable = list;
        }

        public void AutoLinkTables()
        {
            var tmp = SpecialtyList.Cast<BaseItem>().Where(x => x != default).ToList();
            NormalizeCode(tmp);

            var ordered = ParserPool.OrderByDescending(x => x.Year).ToArray();

            var parserLatest = ordered.First(x => !x.IsAlternate);
            if (parserLatest == default)
            {
                Notify("MAIN LIST NOT FOUND");
                return;
            }
            var list = parserLatest.ParseFile().ToArray();
            JoinList(tmp, list, true, x => x.IsSpecial);
            JoinList(tmp, list, true, x => x.PassMarks);
            JoinList(tmp, list, true, x => x.BranchCoef);
            JoinList(tmp, list, true, x => x.Modulus);

            Notify("Fields populated for main list");
            foreach (var hist in ordered.Skip(1))
            {
                var histList = hist.ParseFile().ToArray();
                JoinList(tmp, histList, false, x => x.PassMarks);
            }
            Notify("History set for main list");

            foreach (var item in tmp.Where(x => !x.IsValid()))
            {
                Notify($"Mapping is not valid:  {item.Code} {item.Title} [remote]");
            }

            SpecialtyList = tmp.Cast<SpecialtyInfo>().ToList();
            var altList = ordered.FirstOrDefault(x => x.IsAlternate);
            if (altList != default)
            {

                AlternateList = GetWithAlternateCoefs(altList.ParseFile().Cast<SpecialtyInfo>());
                Notify("Alternate list ready");
            }
            else
            {
                Notify("Alternate list NOT set!");
            }
        }

        private void Notify(string message)
        {
            _logger.Notify(LogArea.Other, LogType.Main, message, true);
        }

        public SpecialtyInfo[] GetWithAlternateCoefs(IEnumerable<SpecialtyInfo> items)
        {
            var tmp = SpecialtyList.Select(x => x.Clone()).ToArray();
            JoinList(tmp, items.ToArray(), false, x => x.Modulus);
            return tmp;
        }

        public SpecialtyInfo[] AlternateList { get; private set; }

        private void NormalizeCode(IEnumerable<BaseItem> tmp)
        {
            foreach (var b in tmp)
            {
                b.Code = SpecialtyCodeNormalizer.Normalize(b.Code);
            }
        }

        private void AutoFindTables()
        {
            ParserPool.Clear();
            foreach (var file in
                Directory.GetFiles(_directoryManager[App.Directories.AddonsCalc], "*",
                    SearchOption.AllDirectories))
            {
                try
                {
                    var ext = new FileInfo(file).Extension;
                    if (IsValidFile(file) && (ext.EndsWith("csv") || ext.EndsWith("txt")))
                    {
                        var parser = GetParserFromFileName(file);
                        _logger.Notify(LogArea.Other, LogType.Info, $"Found [{parser}]=>{file}", true);
                        ParserPool.Add(parser);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.NotifyError(LogArea.Other, ex, true);
                }
            }
        }

        private static bool IsValidFile(string file)
        {
            return _yearReg.IsMatch(file);
        }

        private IBaseItemFileParser GetParserFromFileName(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!_yearReg.IsMatch(name))
                throw new InvalidOperationException("Could not find year in filename");
            var year = int.Parse(_yearReg.Match(name).Groups[0].Value);
            var isAlternate = name.Contains("alt", StringComparison.OrdinalIgnoreCase);
            if (name.Contains("pass", StringComparison.OrdinalIgnoreCase))
            {
                return new ParserV1Lite(year, file, isAlternate);
            }

            if (name.Contains("table", StringComparison.OrdinalIgnoreCase))
            {
                return new ParserV2Full(year, file, isAlternate);
            }

            throw new InvalidOperationException("Unknown file name format");
        }

        private IEnumerable<BaseItem> ParseSpecialItems()
        {
            string path = PathMap[KeyFile.SpecV1];//
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
                            yield return bs;
                        }
                    }
                }
            }
        }

        private void JoinList<T, Prop>(IReadOnlyCollection<T> main, IReadOnlyCollection<T> app, bool strict,
            params Expression<Func<T, Prop>>[] selectors)
            where T : ICodeItem

        {
            var func = selectors.Select((x, i) => new { i, f = x.Compile() }).ToArray();

            foreach (var i in main)
            {
                var _found = false;
                foreach (var j in app)
                {
                    if (!string.Equals(i.Code, j.Code, StringComparison.OrdinalIgnoreCase)) continue;
                    _found = true;
                    var index = 0;
                    foreach (var selector in selectors)
                    {
                        var fv = func.ElementAt(index).f(j);
                        var p = i.GetPropertyInfo(selector);
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
                        else
                            p.SetValue(i, fv);
                        index++;
                    }
                }
                if (strict && !_found)
                {
                    Notify($"{i.Code} not found on binding");
                }
            }

        }

        internal void LoadSpecialtyList(IEnumerable<SpecialtyInfo> list)
        {
            SpecialtyList = list.ToList();
        }

        private async Task FetchSpecialtyInfo()
        {
            RequestAllocator.Instance.MaxAsyncRequests = 3;
            var watch = Stopwatch.StartNew();
            SpecialtyList = new List<SpecialtyInfo>();
            var requestUri = new Uri(_abitUrl + _catalogueUrl);
            try
            {
                var doc = new HtmlDocument();
                using (var r = new Request(requestUri.AbsoluteUri))
                {
                    var s = await r.GetAsync();
                    s.EnsureSuccessStatusCode();
                    doc.LoadHtml(await s.Content.ReadAsStringAsync());
                }
                var mc = doc.DocumentNode.Descendants().First(x => x.HasClass("wk-accordion"));
                var des = mc.Descendants("tr").Where(trd => trd.FirstChild.InnerText != "Код").ToArray();

                int item = 0;
                var tasks = new List<Task>();
                foreach (var el in des)
                {
                    string f = HttpUtility.HtmlDecode(el.Elements("td").First().InnerText);
                    var t = string.Join("", (f ?? throw new InvalidOperationException()).Where(char.IsDigit));
                    var code = int.Parse(t);
                    var content = el.Elements("td").Last();
                    tasks.Add(GetPage(code, content, item++));
                }

                await Task.WhenAll(tasks.ToArray());
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                var g = TimeSpan.FromMilliseconds(elapsedMs);
                Notify($"Parsed specialties.Parse time: {g.TotalSeconds} s");
                if (OnParsed != default)
                    await OnParsed.Invoke();
            }
            catch (Exception ex)
            {
                _logger.NotifyError(LogArea.Other, ex);
                if (OnCacheRequired != default)
                    await OnCacheRequired.Invoke();
            }
        }

        private async Task GetPage(int code, HtmlNode el, int item)
        {
            var linker = el.Element("a").GetAttributeValue("href", "null");

            if (linker != "null")
            {
                if (linker.StartsWith("htt"))
                    linker = linker.Replace(_abitUrl, "");
                var link = _abitUrl + linker;
                using (var request = new Request(link))
                {
                    try
                    {
                        await RequestAllocator.RetryForAsync(request, rm =>
                        {
                            rm.EnsureSuccessStatusCode();
                            ParseSpecialtyPage(rm.Content, linker);
                        }, 10);
                    }
                    catch (Exception ex)
                    {
                        _logger.NotifyError(LogArea.Other, new Exception(
                           string.Join('\n', new string('-', 20), code, HtmlEntity.DeEntitize(el.InnerText).Trim(' '), link,
                               $"Item {item} fetch error: " + ex.Message, new string('-', 20))));
                    }
                }
            }
        }

        private async void ParseSpecialtyPage(HttpContent httpContent, string link)
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
                                var er = el.Element("div");
                                sp.BranchName = new Item
                                {
                                    Content = new List<string>()
                                };
                                sp.BranchName.Content.Add(er.ChildNodes[2].InnerText);
                                var urltmp = er.ChildNodes[2].GetAttributeValue("href", "");
                                sp.BranchName.Url = string.IsNullOrEmpty(urltmp) ? "" : _abitUrl + urltmp;
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
                                            case "text first":
                                                {
                                                    try
                                                    {
                                                        sp.Code = i.ChildNodes[2].InnerText;
                                                    }
                                                    catch (Exception)
                                                    {

#if DEBUG
                                                        Console.WriteLine("Check this url: " + _abitUrl + link);
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
                                                            Url = string.IsNullOrEmpty(urltmp) ? "" : _abitUrl + urltmp,
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
                        sp.SubTitle = sb.CaptalizeFirst();
                    }
                }
                else
                {
                    sp.Title = title;
                }
                sp.Title = sp.Title.Trim().Replace('\n', ' ').Replace("  ", " ").Replace('’', '\'').Replace('`', '\'');
                sp.URL = _abitUrl + link;

                SpecialtyList.Add(sp);
            }
            catch (Exception ex)
            {
                sp.Errors = new List<string>
                {
                    ex.Message + ' ' + Tools.AnonymizeStack(ex.StackTrace)
                };
                SpecialtyList.Add(sp);
            }

#if DEBUG
            Console.WriteLine($"Finished {link}");
#endif
        }

        public IEnumerable<string> GetUniqueSubjectNames()
        {
            if (SpecialtyList.Count == 0)
                return Array.Empty<string>();
            var dist = new List<string>();
            foreach (var t in SpecialtyList)
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
                newer[t] = newer[t].CaptalizeFirst();
            }
            return newer.Where(x => !string.IsNullOrEmpty(x)).Distinct().Reverse().ToArray();
        }

        private bool EqualsAbs(string s1, string s2, bool reverse = false)
        {
            return s1.Contains(s2) ||
             s1.Replace(" і ", " та ").Contains(s2) ||
             s1.Replace('’', '\'').Contains(s2) ||
             s1.Replace('`', '\'').Contains(s2) ||
             !reverse && EqualsAbs(s2, s1, true);
        }
    }
}