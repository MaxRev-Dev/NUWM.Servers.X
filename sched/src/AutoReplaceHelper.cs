using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Utils;

namespace NUWM.Servers.Core.Sched
{
    public sealed class AutoReplaceHelper
    {
        public static AutoReplaceHelper Current;
        public bool Now = false;

        public AutoReplaceHelper()
        {
            Current = this;
        }

        private static readonly string st = "http://nuwm.edu.ua/";

        public async void Run()
        {
            Dictionary = new Dictionary<string, List<string>>();
            try
            {
                using (var request = new Request(st))
                {
                    var resp = await request.GetAsync().ConfigureAwait(false);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                    foreach (var i in doc.DocumentNode.Descendants("div").Where(x => x.HasClass("hvr")))
                    {
                        var node = i.Descendants("a").First();
                        var href = st + node.GetAttributeValue("href", "");
                        new Thread(ParseInstitute).Start(href);

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void ManageAutoReplace()
        {
            var f = "./addons/subjects_parser/autoreplace.txt";
            var fs = SubjectParser.Current.AR;
            var fl = "";
            foreach (var i in fs)
            {
                fl += i.Key + " - " + i.Value + "\n";
            }

            File.WriteAllText(f, fl);
        }

        public static List<string> SmartSearch(string name)
        {
            if (name.Contains('.'))
            {
                name = name.Replace('.', ' ');
            }

            var namesp = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var obj = new List<string>();

            foreach (var i in Dictionary.Values)
            {
                var gf = i.Where(x => x.ToLower().StartsWith(namesp[0].ToLower())).ToArray();
                if (gf.Length > 0)
                {
                    obj.AddRange(gf);
                }
            }

            foreach (var f in namesp.Skip(1))
            {
                if (f == "і" || f == "та")
                {
                    continue;
                }

                var nextGen = new List<string>();
                foreach (var i in obj)
                {
                    if (i.ToLower().Contains(f.ToLower()))
                    {
                        nextGen.Add(i);
                    }
                }

                if (nextGen.Count == 1)
                {
                    if (name.ToLower().Length < nextGen.First().Length)
                    {
                        if (!SubjectParser.Current.AR.ContainsKey(name.ToLower()))
                        {
                            SubjectParser.Current.AR.Add(name.ToLower(), nextGen.First());
                        }

                        return nextGen;
                    }
                }

                obj = nextGen;
            }

            if (obj.Any())
            {
                return obj;
            }

            return new List<string>();
        }

        public async void ParseInstitute(object href)
        {
            try
            {
                var doc = new HtmlDocument();
                using (var request = new Request(href as string))
                {
                    var resp = await request.GetAsync().ConfigureAwait(false);
                    doc.LoadHtml(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                }

                var nodet = doc.GetElementbyId("xb-tree-title-id").Descendants()
                    .Where(x => x.InnerText.ToLower().Contains("кафедри")).ToArray();
                if (nodet.Any())
                {
                    var node = nodet.First();
                    var els = node.Descendants("li");
                    foreach (var i in els)
                    {
                        var hrefx = i.Element("a").GetAttributeValue("href", "");
                        if (hrefx.Contains("javascript"))
                        {
                            continue;
                        }

                        if (!hrefx.StartsWith("http"))
                        {
                            hrefx = st + hrefx;
                        }

                        new Thread(ParseDepartment).Start(hrefx);

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async void ParseDepartment(object href)
        {
            try
            {
                var doc = new HtmlDocument();
                using (var request = new Request(href as string))
                {
                    var resp = await request.GetAsync().ConfigureAwait(false);
                    doc.LoadHtml(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                    if (doc.GetElementbyId("sp") == null)
                    {
                        return;
                    }
                }

                var node = doc.GetElementbyId("sp").Descendants("a")
                    .Where(x => x.InnerText.ToLower().Contains("дисципліни"));
                var f = node.First();
                var hrefx = f.GetAttributeValue("href", "");
                if (hrefx == "#")
                {
                    return;
                }

                if (!hrefx.StartsWith("http"))
                {
                    hrefx = st + hrefx;
                }

                await Task.Run(() => ParsePage(hrefx));

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static Dictionary<string, List<string>> Dictionary;
        private readonly object thisLock = new object();

        public async Task ParsePage(string href)
        {
            try
            {
                var doc = new HtmlDocument();
                using (var request = new Request(href))
                {
                    var resp = await request.GetAsync().ConfigureAwait(false);
                    doc.LoadHtml(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                }

                var node = doc.DocumentNode.Descendants("table").First(x => x.HasClass("sklad"));

                foreach (var i in node.ChildNodes.First().ChildNodes)
                {
                    var subj = i.FirstChild;
                    if (subj.InnerText == "Назва дисципліни")
                    {
                        continue;
                    }

                    var lect = System.Net.WebUtility.HtmlDecode(subj.NextSibling.InnerText)?.TrimEnd(' ')
                        .TrimStart(' ');
                    if (lect != null && lect.Length < 3)
                    {
                        lect = "";
                    }

                    var subject = System.Net.WebUtility.HtmlDecode(subj.InnerText)?.TrimEnd(' ').TrimStart(' ');
                    lock (thisLock)
                    {
                        if (subject != null && subject.Length > 2)
                        {
                            subject = subject.Replace("\"", "'").Replace("  ", " ");
                            if (!Dictionary.ContainsKey(lect ?? throw new InvalidOperationException()))
                            {
                                Dictionary.Add(lect, new List<string> { subject });
                            }
                            else
                            {
                                Dictionary[lect].Add(subject);
                            }

                            Dictionary[lect] = Dictionary[lect].Distinct().ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}