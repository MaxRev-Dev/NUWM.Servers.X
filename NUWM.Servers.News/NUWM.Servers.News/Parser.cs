using HelperUtilties;

using HtmlAgilityPack;
using JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;




namespace Lead
{
    public class ParserPool
    {
        public static string site_url = "http://nuwm.edu.ua";
        public Dictionary<string, Parser> POOL;
        public ParserPool()
        {
            POOL = new Dictionary<string, Parser>();
        }
        public void Wait()
        {
            var u = new ParserPool();
            u.Run();
            Server.Server.CurrentParserPool = u;
        }

        public async void Run()
        {
            GC.Collect();
            StreamReader f = File.OpenText("./addons/news/urls.txt");
            string direct = await f.ReadToEndAsync();
            string[] lines = direct.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            int offset = -1;
            Server.Server.taskDelayM = int.Parse(new Regex(@"(?<=delayM\:)[0-9]*").Match(direct).Groups[0].Value);
            Server.Server.taskDelayH = int.Parse(new Regex(@"(?<=delayH\:)[0-9]*").Match(direct).Groups[0].Value);
            Server.Server.cacheAlive = int.Parse(new Regex(@"(?<=CacheAliveHours\:)[0-9]*").Match(direct).Groups[0].Value);
            Server.Server.pagesDef = int.Parse(new Regex(@"(?<=default_pages_count\:)[0-9]*").Match(direct).Groups[0].Value);
            foreach (var s in lines.Where(x => !x.StartsWith("#")))
            {
                string[] strs = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string news_url = strs[0];
                int unid = -100;
                if (strs.Count() > 1)
                {
                    unid = int.Parse(new Regex(@"(?<=id\:)[0-9]*").Match(strs[1]).Groups[0].Value);
                }
                string key = null;
                if (!news_url.Contains("university"))
                {
                    var p = news_url.Substring(0, news_url.LastIndexOf('/'));
                    key = p.Substring(p.LastIndexOf('/') + 1);
                }
                else key = news_url.Substring(news_url.LastIndexOf('/') + 1);
                Parser parser = new Parser(news_url, key, unid, offset += 1) { CacheEpoch = 0 };
                POOL.Add(key, parser);
                await LoadNewsCache(key, parser);
                new Thread(new ParameterizedThreadStart(parser.ParsePages)).Start(parser);
            }


            GC.Collect();
        }
        async Task LoadNewsCache(string key, Parser parser)
        {
            try
            {
                var f = "./cache/news_" + key + ".txt";
                if (File.Exists(f))
                {
                    var t = File.OpenText(f);
                    var u = JsonConvert.DeserializeObject<List<NewsItem>>(await t.ReadToEndAsync());
                    parser.newslist = u;

                }
            }
            catch (Exception)
            {

            }
        }
        public class CacheUpdater
        {
            static System.Timers.Timer timer;
            static void Timer_Elapsed(object sender, ElapsedEventArgs e)
            {
                timer.Stop();
                new Thread(CheckForUpdates).Start();
                Schedule_Timer();
            }

            public static void CheckForUpdates()
            {
                foreach (var i in Server.Server.CurrentParserPool.POOL.Values)
                    new Thread(new ParameterizedThreadStart(UpdateParser)).Start(i);

            }
            private static void UpdateParser(object obj)
            {
                try
                {
                    var p = obj as Parser;
                    foreach (var u in p.newslist)
                    {
                        if ((TimeChron.GetRealTime() - new DateTime(long.Parse(u.CachedOnStr)))
                            .Hours > Server.Server.cacheAlive)
                        {
                            u.Detailed = new NewsItem.NewsItemDetailed(u);
                        }
                    }
                }
                catch (Exception) { }
            }
            public static DateTime scheduledTime;
            public static void Schedule_Timer()
            {

                DateTime nowTime = TimeChron.GetRealTime();

                scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, 0, 0).AddHours(1);

                if (nowTime > scheduledTime)
                {
                    scheduledTime = scheduledTime.AddHours(12);
                }

                double tickTime = (scheduledTime - TimeChron.GetRealTime()).TotalMilliseconds;
                timer = new System.Timers.Timer(tickTime);
                timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
                timer.Start();
            }
        }

        public class Parser
        {
            string url = null;
            public string xkey;
            public int pagesDef = 15;
            int offsetMins;
            HtmlDocument CurrentDoc;
            public int InstituteID;
            public int CacheEpoch { get; set; }
            public static List<NewsItem> InstantCache = new List<NewsItem>();
            public PoolParserScheduler scheduler;
            public Parser(string url, string key, int institute_id = -100, int offset = 0)
            {
                xkey = key;
                offsetMins = offset;
                InstituteID = institute_id;
                this.url = url;
                newslist = new List<NewsItem>();
            }
            public enum InstantState
            {
                Success, TimedOut, ErrorParsing, ConnectionWithServerError,
                FromCache
            }

            public static async void LoadInstantCache()
            {
                if (File.Exists("./cache/instantCache.txt"))
                    try
                    {
                        InstantCache = JsonConvert.DeserializeObject<List<NewsItem>>
                            (await File.OpenText("./cache/instantCache.txt").ReadToEndAsync());
                    }
                    catch (Exception) { }
            }
            public class ScheduleInstantCacheSave
            {
                static System.Timers.Timer timer;
                static void Timer_Elapsed(object sender, ElapsedEventArgs e)
                {
                    timer.Stop();
                    new Thread(SaveInstantCache).Start();
                    Schedule_Timer();
                }

                public static void SaveInstantCache()
                {
                    var g = File.CreateText("./cache/instantCache.txt");
                    g.WriteAsync(JsonConvert.SerializeObject(InstantCache)).Wait();
                    g.Close();

                    var d = DateTime.Now;
                    var gg = File.CreateText("./cache/clientLog" + d.ToLongTimeString() + ".txt");
                    foreach (var i in Server.Server.log)
                        gg.WriteLine(i);
                    Server.Server.log.Clear();
                    gg.Close();
                }

                public static DateTime scheduledTime;
                public static void Schedule_Timer()
                {

                    DateTime nowTime = TimeChron.GetRealTime();

                    scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, 0, 0).AddMinutes(30);

                    if (nowTime > scheduledTime)
                    {
                        scheduledTime = scheduledTime.AddHours(12);
                    }

                    double tickTime = (scheduledTime - TimeChron.GetRealTime()).TotalMilliseconds;
                    timer = new System.Timers.Timer(tickTime);
                    timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
                    timer.Start();
                }
            }

            public async static Task<Tuple<NewsItem, InstantState>> ParsePageInstant(string url, bool html)
            {
                if (InstantCache == null) InstantCache = new List<NewsItem>();
                NewsItem obj = null; InstantState state = InstantState.Success;
                if (InstantCache != null && InstantCache.Count > 0)
                {
                    var spl = url.Substring(url.LastIndexOf('/'));
                    if (url.EndsWith('/')) url.Substring(0, url.Length - 2);
                    foreach (var i in Server.Server.CurrentParserPool.POOL.Values)
                    {
                        var u = i.newslist.Where(x => x.Url.Contains(spl));
                        if (u.Count() == 1)
                        {
                            obj = DeepCopy(u.First());
                            obj.Detailed.ContentHTML = html ? u.First().Detailed.ContentHTML : u.First().GetText();

                            return new Tuple<NewsItem, InstantState>(obj, state);
                        }
                    }
                    state = InstantState.FromCache;
                    var op = InstantCache.Where(x => x.Url.Contains(spl));
                    if (op.Count() == 1)
                    {
                        obj = DeepCopy(op.First());
                        obj.Detailed.ContentHTML = html ? op.First().Detailed.ContentHTML : op.First().GetText();

                        return new Tuple<NewsItem, InstantState>(obj, state);
                    }
                }

                try
                {
                    HttpResponseMessage rm = null;
                    try { rm = await (new CreateClientRequest(url).GetAsync()); }
                    catch (Exception) { return new Tuple<NewsItem, InstantState>(null, InstantState.ConnectionWithServerError); }
                    if (rm != null && rm.IsSuccessStatusCode && rm.Content != null)
                    {
                        var doc = new HtmlDocument();
                        var tg = await rm.Content.ReadAsStringAsync();
                        doc.LoadHtml(tg);
                        var item = new NewsItem();
                        var dla = doc.DocumentNode.Descendants("article").First();
                        item.Title = dla.Element("h1").InnerText;
                        item.Date = dla.Element("time").InnerText;
                        var imgp = dla.Element("img").Attributes["src"].Value;
                        item.ImageURL = site_url + imgp.Substring(1, imgp.Length - 1);
                        item.Url = url;
                        NewsItem.NewsItemDetailed.ParseArticle(item, doc.DocumentNode.Descendants().Where(x =>
                         x.Name == "article" && x.HasClass("item-detailed")).First());
                        InstantCache.Add(item);

                        var ret = DeepCopy(item);
                        ret.Detailed.ContentHTML = html ? item.Detailed.ContentHTML : item.GetText();
                        return new Tuple<NewsItem, InstantState>(ret, InstantState.Success);
                    }
                    else
                    {
                        return new Tuple<NewsItem, InstantState>(null, InstantState.TimedOut);
                    }
                }
                catch (Exception)
                {
                    return new Tuple<NewsItem, InstantState>(null, InstantState.ErrorParsing);
                }

            }
            public async void ParsePages(object parser)
            {
                try
                {
                    var urlx = (parser as Parser).url;
                    CurrentDoc = new HtmlDocument();

                    CreateClientRequest clientRequest = new CreateClientRequest(urlx);
                    CurrentDoc.LoadHtml(await (await clientRequest.GetAsync()).Content.ReadAsStringAsync());

                    var news_art = CurrentDoc.DocumentNode.Descendants().Where(x => x.HasClass("news") && x.HasClass("list")).Single();
                    if (newslist == null) newslist = new List<NewsItem>();
                    var op = ParseInstance(new Tuple<HtmlNode, int, string>(news_art, 1, url));
                    if (newslist.Count > 0)
                    {
                        var opf = op.Where(x => newslist.Where(y => x.Url == y.Url).Count() == 0);
                        newslist.AddRange(opf);
                    }
                    else newslist.AddRange(op);

                    foreach (var t in op.Where(x => x.Detailed == null || x.Detailed == new NewsItem.NewsItemDetailed()))
                    {
                        new Thread(new ParameterizedThreadStart(PageThread)).Start(t);
                    }
                    int pages_count = Convert.ToInt16(news_art.NextSibling.ChildNodes[news_art.NextSibling.ChildNodes.Count - 4].InnerText);
                    pagesDef = pages_count < Server.Server.pagesDef ? pages_count : Server.Server.pagesDef;

#if DEBUG
                    Trace.WriteLine("DEBUG");
#else
                    for (int id = 2; id < pagesDef + 1; id++)
                    {
                        var str = await (await (new CreateClientRequest
                            (string.Format(urlx + "?p={0}", id))).
                            GetAsync()).Content.ReadAsStringAsync();

                        CurrentDoc = new HtmlAgilityPack.HtmlDocument();
                        CurrentDoc.LoadHtml(str);

                        news_art = CurrentDoc.DocumentNode.Descendants().Where(x => x.HasClass("news") && x.HasClass("list")).Single();
                        var u = ParseInstance(new Tuple<HtmlAgilityPack.HtmlNode, int, string>(news_art, id, url));

                        var opf = u.Where(x => newslist.Where(y => x.Url == y.Url).Count() == 0);
                        newslist.AddRange(opf);

                        foreach (var t in u)
                        {
                            new Thread(new ParameterizedThreadStart(PageThread)).Start(t);
                        }
                    }
#endif

                }
                catch (Exception) { }

                if (scheduler == null)
                {
                    scheduler = new PoolParserScheduler(xkey);
                    scheduler.Schedule_Timer(xkey, new TimeSpan(), offsetMins, InstituteID);
                }
            }
            public static void PageThread(Object item)
            {
                new NewsItem.NewsItemDetailed(item as NewsItem);
            }
            public static List<NewsItem> ParseInstance(Tuple<HtmlAgilityPack.HtmlNode, int, string> articles)
            {
                List<NewsItem> CurrentPageNews = new List<NewsItem>();
                foreach (var i in articles.Item1.ChildNodes)
                {
                    var wrimg = i.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    var img = site_url + wrimg.Attributes["src"].Value;
                    var desc = i.ChildNodes[1].ChildNodes;

                    var date = desc[0].InnerText;
                    var title = desc[1].InnerText;
                    var desc_text = desc[2].InnerText;
                    var read = desc[3].ChildNodes[0].Attributes["href"].Value;
                    var url = articles.Item3;
                    if (read.Contains(url))
                    {
                        var t = read.Split(url, StringSplitOptions.RemoveEmptyEntries).First();
                        if (t.Count() == 1)
                            continue;
                    }
                    CurrentPageNews.Add(new NewsItem()
                    {
                        Excerpt = WebUtility.HtmlDecode(desc_text),
                        ImageURL = img,
                        Title = title,
                        Date = date,
                        Url = read,
                        PageId = articles.Item2
                    });
                }
                return CurrentPageNews;
            }
            public static void SaveCache(string key = "")
            {
                if (key == "")
                    foreach (var ParserX in Server.Server.CurrentParserPool.POOL.Keys)
                        Saver(ParserX);
                else
                    Saver(key);
            }
            private async static void Saver(string ParserX)
            {
                var ig = Server.Server.CurrentParserPool.POOL[ParserX];
                if (!Directory.Exists("./cache"))
                    Directory.CreateDirectory("./cache");
                var toper = File.CreateText("./cache/news_" + ParserX + ".txt");
                await toper.WriteAsync(JsonConvert.SerializeObject(ig.newslist));
                toper.Close();
            }
            public class PoolParserScheduler
            {
                public PoolParserScheduler(string par)
                {
                    ParserX = par;
                }
                string ParserX { get; set; }
                System.Timers.Timer timer;
                private int id = -100, offset;
                void Timer_Elapsed(object sender, ElapsedEventArgs e)
                {
                    timer.Stop();

                    if (Server.Server.CheckForInternetConnection())
                    {
                        Saver(ParserX);
                        var t = Server.Server.CurrentParserPool.POOL[ParserX];
                        offset = t.offsetMins;
                        (Server.Server.CurrentParserPool.POOL[ParserX] =
                             new Parser(t.url, ParserX, t.InstituteID, t.offsetMins) { CacheEpoch = ++t.CacheEpoch }).ParsePages(t);
                        t.CacheEpoch++;
                    }
                    else
                    {
                        Schedule_Timer(ParserX);
                    }
                }
                public DateTime scheduledTime;
                public void Schedule_Timer(string xkey, TimeSpan timeToSleep = new TimeSpan(), int Offset = 0, int ID = 0)
                {
                    if (ID != 0) id = ID;
                    DateTime nowTime = TimeChron.GetRealTime();

                    if (timeToSleep == new TimeSpan())
                    {
                        int delayMins = 0, delayHours = 0;
                        if (id != -100)
                            delayHours = Server.Server.taskDelayH;
                        else delayMins = Server.Server.taskDelayM;
                        timeToSleep = new TimeSpan(delayHours, delayMins, 0);
                    }
                    scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, 0, 0).
                        AddHours(timeToSleep.Hours).AddMinutes(timeToSleep.Minutes +
                        ((Server.Server.CurrentParserPool.POOL[xkey].CacheEpoch == 0) ? Offset : 0));

                    if (nowTime > scheduledTime)
                    {
                        scheduledTime = scheduledTime.AddHours(12);
                    }

                    double tickTime = (scheduledTime - TimeChron.GetRealTime()).TotalMilliseconds;
                    timer = new System.Timers.Timer(tickTime);
                    timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
                    timer.Start();
                }
            }

            [field: NonSerialized]
            public List<NewsItem> newslist;
            public class NewsItemVisualizer
            {
                [JsonProperty("item")]
                public List<NewsItem> NewsItemList { get; set; }
            }


            public static T DeepCopy<T>(T other)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(ms, other);
                    ms.Position = 0;
                    return (T)formatter.Deserialize(ms);
                }
            }
        }
    }
}

namespace JSON
{
    using static Lead.ParserPool;
    public partial class NewsItem
    {
        public string GetText()
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(@"<!DOCTYPE html><html><head></head><body></body></html>");
            if (Detailed == null) return "wait for minute";
            HtmlNode node = new HtmlNode(HtmlNodeType.Element, doc, 0)
            {
                InnerHtml = Detailed.ContentHTML
            };

            return node.InnerText;
        }
        [Serializable]
        public partial class NewsItemDetailed
        {
            public NewsItemDetailed() { }
            public NewsItemDetailed(NewsItem item)
            {
                CreateClientRequest request = new CreateClientRequest(item.Url);

                HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
                var y = request.GetAsync();
                y.Wait();
                if (y.IsCompletedSuccessfully)
                {
                    if (y.Result != null)
                    {
                        doc.LoadHtml(y.Result.Content.ReadAsStringAsync().Result);
                        ParseArticle(item, doc.DocumentNode.Descendants().Where(x =>
                         x.Name == "article" && x.HasClass("item-detailed")).First());
                    }
                    else
                    {
                        y = request.GetAsync();
                        y.Wait();
                        if (y.IsCompletedSuccessfully && y.Result != null)
                        {
                            doc.LoadHtml(y.Result.Content.ReadAsStringAsync().Result);
                            ParseArticle(item, doc.DocumentNode.Descendants().Where(x =>
                             x.Name == "article" && x.HasClass("item-detailed")).First());
                        }
                        else
                        {
                            item.Detailed = null;
                        }
                    }
                }
            }

            public static void ParseArticle(NewsItem item, HtmlNode artc)
            {
                item.Detailed = new NewsItem.NewsItemDetailed();
                if (string.IsNullOrEmpty(item.Date))
                {
                    item.Date = artc.Descendants("time").First().InnerText;
                    var img = artc.Descendants("img").First().GetAttributeValue("src", "");
                    item.ImageURL = site_url + img.Substring(1);
                }
                #region Docs
                var docs = artc.Descendants().Where(x => x.HasAttributes && x.GetAttributeValue("class", "null").Contains("file-desc"));
                if (docs.Any())
                {
                    item.Detailed.DocsLinks = new List<string[]>();
                    foreach (var doc in docs)
                    {
                        var f = doc.NextSibling;
                        var t = f.ChildNodes[0].GetAttributeValue("data-href", "");
                        item.Detailed.DocsLinks.Add(new[] { doc.InnerText, @"" + WebUtility.UrlEncode(t) });
                    }
                }
                #endregion
                #region Images
                var imgnode = artc.Descendants().Where(x =>
                x.Name == "div" && x.HasClass("s1") && x.GetAttributeValue("role", "") == "marquee");


                if (imgnode.Count() > 0)
                {
                    item.Detailed.ImagesLinks = new List<string>();
                    foreach (var y in imgnode)
                    {
                        var box = y.ChildNodes.Where(x => x.HasClass("box")).Single();
                        foreach (var i in box.FirstChild.ChildNodes)
                        {
                            string uri = i.FirstChild.Attributes["src"].Value;
                            uri = uri.Replace(new Regex(@"(?<=photo.).*(?=\/)").Match(uri).Value, "800x800");
                            item.Detailed.ImagesLinks.Add(site_url + uri.Substring(1));
                        }
                    }
                }
                #endregion
                #region RelatedLink
                var rel = artc.Descendants().Where(x => x.InnerText.Contains("Читайте також"));
                if (rel.Count() > 0)
                {
                    var y = rel.First().Descendants("a");
                    if (y.Count() > 0 && y.First().HasAttributes)
                        item.RelUrl = WebUtility.UrlEncode(site_url + "/" + y.Last().Attributes["href"].Value);
                }
                #endregion

                #region Text
                var text = artc.Descendants().Where(x => x.HasAttributes && x.GetAttributeValue("id", "").Contains("item-desc")).First();
                var xr = text.Descendants().Where(x => (x.InnerHtml.Contains("id=\"gallery") || x.HasClass("back") || x.HasAttributes && x.GetAttributeValue("role", "") == "photo"));
                if (xr.Any())
                {
                    for (var i = 0; i < xr.Count(); i++)
                        if (text.ChildNodes.Contains(xr.ElementAt(i)))
                            text.RemoveChild(xr.ElementAt(i));
                }

                item.Detailed.ContentHTML = @"" + (text.OuterHtml.Replace("%22", "%5C%22"));

                #endregion
            }

        }
    }
}
