using HtmlAgilityPack;
using JSON;
using MR.Servers.Utils;
using Newtonsoft.Json;
using NUWM.Servers.Core.News;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Tools = MR.Servers.Utils.Tools;

namespace Lead
{

    public class ParserPool
    {
        public static ParserPool Current;
        public static string site_url = "http://nuwm.edu.ua",
                             site_abit_url = "http://start.nuwm.edu.ua";
        private ConcurrentDictionary<string, Parser> ppoll = null;
        public ConcurrentDictionary<string, Parser> POOL => ppoll ?? (ppoll = new ConcurrentDictionary<string, Parser>());
        public void SetCurrent(ParserPool u = null)
        {
            Current = u ?? this;
        }

        public async void BaseInitParsers()
        {
            if (Current.POOL == new ConcurrentDictionary<string, Parser>())
            {
                var u = new ParserPool();
                await u.Run();
                SetCurrent(u);
            }
            else
            {
                await Current.Run();
            }
        }
        private async Task<string[]> InitSettings()
        {
            var file = Path.Combine(MainApp.GetApp.Server.DirectoryManager[MainApp.Dirs.AddonsNews], "urls.txt");
            while (!File.Exists(file))
            {
                Console.WriteLine("Urls missing!");
                await Task.Delay(5000);
                
            }

            StreamReader f = File.OpenText(file);

            string direct = await f.ReadToEndAsync();
            string[] lines = direct.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            MainApp.taskDelayM = int.Parse(new Regex(@"(?<=delayM\:)[0-9]*").Match(direct).Groups[0].Value);
            MainApp.taskDelayH = int.Parse(new Regex(@"(?<=delayH\:)[0-9]*").Match(direct).Groups[0].Value);
            MainApp.cacheAlive = int.Parse(new Regex(@"(?<=CacheAliveHours\:)[0-9]*").Match(direct).Groups[0].Value);
            MainApp.pagesDef = int.Parse(new Regex(@"(?<=default_pages_count\:)[0-9]*").Match(direct).Groups[0].Value);
            return lines;
        }
        public async Task Run()
        {
            GC.Collect();
            string[] lines = await InitSettings();
            int offset = -1;
            Parser.LoadInstantCache();
            try
            {
                foreach (var s in lines.Where(x => !x.StartsWith("#")))
                {
                    string[] strs = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string news_url = strs[0];
                    int unid = -100;
                    if (strs.Count() > 1)
                    {
                        unid = int.Parse(new Regex(@"(?<=id\:)[0-9]*").Match(strs[1]).Groups[0].Value);
                    }
                    string key = null; bool abit = false;
                    if (news_url.Contains("start.nuwm.edu.ua"))
                    {
                        if (news_url.Contains("kolonka-novyn"))
                        {
                            key = "abit-news";
                        }
                        else if (news_url.Contains("oholoshennia"))
                        {
                            key = "abit-ads";
                        }

                        abit = true;
                    }
                    else if (!news_url.Contains("university"))
                    {
                        var p = news_url.Substring(0, news_url.LastIndexOf('/'));
                        key = p.Substring(p.LastIndexOf('/') + 1);
                    }
                    else
                    {
                        key = news_url.Substring(news_url.LastIndexOf('/') + 1);
                    }

                    if (key.Equals("nuwm.edu.ua"))
                    {
                        key = string.Join("", strs[0].Substring(strs[0].LastIndexOf('/') + 1).Split(new char[] { '-', '_' }).Select(x => x[0].ToString()));
                    }

                    if (key.Contains("zaochno-distanc"))
                    {
                        key = "zdn";
                    }

                    new Task(new Action(async () =>
                    {
                        Parser parser = new Parser(news_url, key, abit, unid, offset += 1) { CacheEpoch = 0 };
                        try
                        {
                            if (!POOL.TryAdd(key, parser))
                            {
                                await LoadNewsCache(key, parser);
                                POOL[key] = parser;
                            }
                        }
                        catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }
                        finally
                        {
                            if (parser.Newslist.Count == 0)
                            {
                                await LoadNewsCache(key, parser);
                            }

                            RunParseThread(parser);
                        }
                    })).Start();
                }
            }
            catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }
            GC.Collect();
        }
        private void RunParseThread(Parser parser)
        {
            try
            {
                new Thread(new ParameterizedThreadStart(parser.ParsePagesParallel)).Start(parser);
            }
            catch { }
        }

        private async Task LoadNewsCache(string key, Parser parser)
        {
            try
            {
                var f = Path.Combine(MainApp.GetApp.Server.DirectoryManager[MainApp.Dirs.Cache],"news_" + key + ".txt");
                if (File.Exists(f))
                {
                    await File.OpenText(f).ReadToEndAsync().ContinueWith(t =>
                    {
                        var r = JsonConvert.DeserializeObject<List<NewsItem>>(t.Result);
                        if (r != null)
                        {
                            parser.Newslist = r;
                        }
                    });
                }
            }
            catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }
        }

        public class CacheUpdater
        {
            public static CacheUpdater Current;
            private static System.Timers.Timer timer;
            public CacheUpdater()
            {
                Current = this;
                SetDelay(new TimeSpan(1, 0, 0)); Schedule_Timer();
            }

            private void Timer_Elapsed(object sender, ElapsedEventArgs e)
            {
                timer.Stop();
                new Thread(CheckForUpdates).Start();
                Schedule_Timer();
            }

            public void CheckForUpdates()
            {
                foreach (var i in ParserPool.Current.POOL.Values)
                {
                    new Thread(new ParameterizedThreadStart(UpdateParser)).Start(i);
                }
            }
            private static void UpdateParser(object obj)
            {
                try
                {
                    var p = obj as Parser;
                    foreach (var u in p.Newslist)
                    {
                        if ((TimeChron.GetRealTime() - new DateTime(long.Parse(u.CachedOnStr)))
                            .Hours > MainApp.cacheAlive)
                        {
                            u.Detailed = new NewsItem.NewsItemDetailed(u);
                        }
                    }
                }
                catch (Exception) { }
            }
            public static DateTime ScheduledTime;
            protected void SetDelay(TimeSpan timeSpan)
            {
                CurrentDelay = new TimeSpan(timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, 0);
            }

            public void StopTimer()
            {
                timer.Stop();
            }

            public TimeSpan CurrentDelay { get; private set; }

            /// <summary>
            /// Timer sets here
            /// </summary>
            public void Schedule_Timer()
            {
                DateTime nowTime = TimeChron.GetRealTime();
                // Debug
                // scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day,nowTime.Hour,nowTime.Minute, 0, 0).AddMinutes(1);
                TimeSpan CountHourTop()
                {
                    var timeOfDay = TimeChron.GetRealTime().TimeOfDay;
                    var nextFullHour = TimeSpan.FromHours(Math.Ceiling(timeOfDay.TotalHours));
                    return nextFullHour - timeOfDay;
                }
                TimeSpan CountMinuteTop()
                {
                    var timeOfDay = TimeChron.GetRealTime().TimeOfDay;
                    var nextFullMinute = TimeSpan.FromMinutes(Math.Ceiling(timeOfDay.TotalMinutes));
                    return nextFullMinute - timeOfDay;
                }
                var d = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, 0);
                d = (CurrentDelay.Hours != 0) ? d.Add(CountHourTop()) : d;
                if (CurrentDelay.Hours != 0 && (d - nowTime).TotalHours > CurrentDelay.Hours)
                {
                    d = d.AddHours(-CurrentDelay.Hours);
                }

                d = (CurrentDelay.Minutes != 0) ? d.Add(CountMinuteTop()) : d;
                if (CurrentDelay.Minutes != 0 && (d - nowTime).TotalMinutes > CurrentDelay.Minutes)
                {
                    d = d.AddMinutes(-CurrentDelay.Minutes);
                }

                ScheduledTime = new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0);

                while (nowTime > ScheduledTime)
                {
                    ScheduledTime = ScheduledTime.AddMinutes(5);
                }

                double tickTime = (ScheduledTime - TimeChron.GetRealTime()).TotalMilliseconds;
                timer = new System.Timers.Timer(tickTime);
                timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
            }
        }

        public class Parser
        {
            private string url = null;
            private bool abitParser = false;
            public string xkey;
            public int pagesDef = 15;
            private int offsetMins;
            private HtmlDocument CurrentDoc;
            public int InstituteID;
            public int CacheEpoch { get; set; }
            public PoolParserScheduler scheduler;

            public Parser(string url, string key, bool abitParser, int institute_id = -100, int offset = 0)
            {
                xkey = key;
                offsetMins = offset;
                this.abitParser = abitParser;
                InstituteID = institute_id;
                this.url = url;
                newslist = new List<NewsItem>();
            }

            #region InstantCacheImpl
            public enum InstantState
            {
                Success, TimedOut, ErrorParsing, ConnectionWithServerError,
                FromCache
            }
            public static List<NewsItem> InstantCache = new List<NewsItem>();
            public static void SaveInstantCache()
            {
                var f = Path.Combine(
                    MainApp.GetApp.Server.DirectoryManager[MainApp.Dirs.Cache], "instantCache.txt");
                try
                {
                    if (InstantCache != null && InstantCache.Count > 0)
                    {
                        File.WriteAllTextAsync(f, JsonConvert.SerializeObject(InstantCache));
                    }
                }
                catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }
            }
            public static async void LoadInstantCache()
            {
                if (File.Exists(Path.Combine(
                    MainApp.GetApp.Server.DirectoryManager[MainApp.Dirs.Cache], "instantCache.txt")))
                {
                    try
                    {
                        InstantCache = JsonConvert.DeserializeObject<List<NewsItem>>
                            (await File.OpenText("./cache/instantCache.txt").ReadToEndAsync());
                    }
                    catch (Exception) { }
                }
            }
            public class ScheduleInstantCacheSave
            {
                private static System.Timers.Timer timer;

                private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
                {
                    timer.Stop();
                    new Thread(SaveInstantCache).Start();
                    Schedule_Timer();
                }

                public static void SaveInstantCache()
                {
                    var g = File.CreateText(Path.Combine(
                    MainApp.GetApp.Server.DirectoryManager[MainApp.Dirs.Cache], "instantCache.txt"));
                    g.WriteAsync(JsonConvert.SerializeObject(InstantCache)).Wait();
                    g.Close();
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
            public static async Task<Tuple<NewsItem, InstantState>> ParsePageInstant(string url, bool html)
            {
                try
                {

                    if (InstantCache == null)
                    {
                        InstantCache = new List<NewsItem>();
                    }

                    NewsItem obj = null; InstantState state = InstantState.FromCache;
                    if (InstantCache != null && InstantCache.Count > 0)
                    {
                        var spl = url.Substring(url.LastIndexOf('/'));
                        if (url.EndsWith('/'))
                        {
                            url.Substring(0, url.Length - 2);
                        }

                        foreach (var i in Current.POOL.Values)
                        {
                            var u = i.newslist.Where(x => x.Url.Contains(spl));
                            if (u.Count() == 1)
                            {
                                obj = DeepCopy(u.First());
                                obj.Detailed.ContentHTML = html ? u.First().Detailed.ContentHTML : u.First().GetText();

                                return new Tuple<NewsItem, InstantState>(obj, state);
                            }
                        }
                        var op = InstantCache.Where(x => x.Url.Contains(spl));
                        if (op.Count() == 1)
                        {
                            obj = DeepCopy(op.First());
                            obj.Detailed.ContentHTML = html ? op.First().Detailed.ContentHTML : op.First().GetText();

                            return new Tuple<NewsItem, InstantState>(obj, state);
                        }
                    }

                }
                catch
                {
                    return new Tuple<NewsItem, InstantState>(null, InstantState.ErrorParsing);
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
                        var imgp = dla.Element("img").GetAttributeValue("src", "");
                        item.ImageURL = site_url +
                            imgp.Substring(1, imgp.Length - 1);
                        item.Url = url;
                        NewsItem.NewsItemDetailed.ParseArticle(item, doc.DocumentNode.Descendants().Where(x =>
                         x.Name == "article" && x.HasClass("item-detailed")).First());
                        InstantCache.Add(item);

                        var ret = DeepCopy(item);
                        ret.Detailed.ContentHTML = html ? ret.Detailed.ContentHTML : ret.GetText();
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
            #endregion

            public async void ParsePagesParallel(object parser_obj)
            {
                await ParsePages(parser_obj);
            }

            public async Task ParsePages(object parser_obj)
            {
                var parser = (parser_obj as Parser);
                parser.scheduler = null;
                if (!abitParser)
                {
                    try
                    {
                        var urlx = parser.url;
                        CurrentDoc = new HtmlDocument();

                        CreateClientRequest clientRequest = new CreateClientRequest(urlx);
                        CurrentDoc.LoadHtml(await (await clientRequest.GetAsync()).Content.ReadAsStringAsync());

                        var news_art = CurrentDoc.DocumentNode.Descendants().Where(x => x.HasClass("news") && x.HasClass("list")).Single();
                        if (newslist == null)
                        {
                            newslist = new List<NewsItem>();
                        }

                        var op = ParseInstance(new Tuple<HtmlNode, int, string>(news_art, 1, url));
                        if (newslist.Count > 0)
                        {
                            newslist.AddRange(op.Where(x => newslist.Where(y => x.Url == y.Url).Count() == 0));
                        }
                        else
                        {
                            newslist.AddRange(op);
                        }

                        foreach (var t in op.Where(x => x.Detailed == null || x.Detailed == new NewsItem.NewsItemDetailed()))
                        {
                            new Thread(new ParameterizedThreadStart(PageThread)).Start(t);
                        }

                        int pages_count = Convert.ToInt16(news_art.NextSibling.ChildNodes[news_art.NextSibling.ChildNodes.Count - 4].InnerText);
                        pagesDef = pages_count < MainApp.pagesDef ? pages_count : MainApp.pagesDef;

                        ///#if DEBUG
                        ///                       Trace.WriteLine("DEBUG");
                        ///#else
                        for (int id = 2; id < pagesDef + 1; id++)
                        {
                            var str = await (await (new CreateClientRequest
                                (string.Format(urlx + "?p={0}", id))).
                                GetAsync()).Content.ReadAsStringAsync();

                            CurrentDoc = new HtmlAgilityPack.HtmlDocument();
                            CurrentDoc.LoadHtml(str);

                            news_art = CurrentDoc.DocumentNode.Descendants().Where(x => x.HasClass("news") && x.HasClass("list")).Single();
                            var u = ParseInstance(new Tuple<HtmlAgilityPack.HtmlNode, int, string>(news_art, id, url));

                            newslist.AddRange(u.Where(x => newslist.Where(y => x.Url == y.Url).Count() == 0));

                            foreach (var t in u)
                            {
                                new Thread(new ParameterizedThreadStart(PageThread)).Start(t);
                            }
                        }
                        try
                        {
                            newslist = newslist.OrderByDescending(x => DateTime.ParseExact(x.Date, "dd MMMM yyyy", CultureInfo.CreateSpecificCulture("uk-UA"))).ToList();
                        }
                        catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }
                        //#endif
                    }
                    catch (Exception) { }
                }
                else
                {
                    // ABIT PAGES
                    newslist.AddRange(await AbitNewsParser(parser));
                    try
                    {

                        //newslist = newslist.OrderByDescending(x => 
                        //DateTime.Parse(x.Date.Substring(x.Date.IndexOf(',')).Trim(), new CultureInfo("uk-UA"))
                        //   ).ToList();
                    }
                    catch (Exception ex) {
                        MainApp.GetApp.Server.Logger.NotifyError(ex); }
                }

                // Fix of server connection problems)
                try
                {
                    if (newslist.Count == 0)
                    {
                        newslist = DeepCopy(parser.newslist);
                    }
                }
                catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }

                if (scheduler == null)
                {
                    scheduler = new PoolParserScheduler(xkey);
                    scheduler.Schedule_Timer(xkey, new TimeSpan(), offsetMins, InstituteID);
                }
            }


            private async Task<List<NewsItem>> AbitNewsParser(Parser parser)
            {
                List<NewsItem> items = new List<NewsItem>();
                try
                {
                    CreateClientRequest request = new CreateClientRequest(parser.url);
                    var r = await request.GetAsync();
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(await r.Content.ReadAsStringAsync());
                    var it = doc.DocumentNode.Descendants("div").Where(x => x.HasClass("pagination"));
                    if (it.Any())
                    {
                        var its = it.First();
                        foreach (var i in its.Descendants("a").Where(x => x.GetAttributeValue("class", "") == ""))
                        {
                            CreateClientRequest rq = new CreateClientRequest(site_abit_url + i.GetAttributeValue("href", ""));
                            var v = await rq.GetAsync();
                            HtmlDocument docx = new HtmlDocument();
                            docx.LoadHtml(await v.Content.ReadAsStringAsync());
                            items.AddRange(await AbitPageItems(docx));
                        }
                    }
                    // Also parsing current first page
                    items.AddRange(await AbitPageItems(doc));
                }
                catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }
                return items;
            }
            private async Task<List<NewsItem>> AbitPageItems(HtmlDocument docx)
            {
                List<NewsItem> items = new List<NewsItem>();
                try
                {
                    foreach (var ids in docx.GetElementbyId("k2Container").Descendants().Where(x => x.HasClass("catItemTitle")))
                    {
                        var url = site_abit_url + ids.Element("a").GetAttributeValue("href", "");
                        CreateClientRequest rq1 = new CreateClientRequest(url);
                        var v1 = await rq1.GetAsync();
                        HtmlDocument docx1 = new HtmlDocument();
                        docx1.LoadHtml(await v1.Content.ReadAsStringAsync());
                        items.AddRange(AbitPage(docx1, url));
                    }
                }
                catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }
                return items;
            }
            private List<NewsItem> AbitPage(HtmlDocument doc, string url)
            {
                List<NewsItem> items = new List<NewsItem>();
                try
                {
                    var i = doc.GetElementbyId("k2Container");
                    var txt = i.Descendants().Where(x => x.HasClass("itemFullText")).First();
                    var title = i.Descendants().Where(x => x.HasClass("itemTitle")).First().InnerHtml;
                    var date = i.Descendants().Where(x => x.HasClass("itemDateCreated")).First().InnerHtml;
                    var im = i.Descendants().Where(x => x.HasClass("itemImage"));
                    string img = null;
                    if (im.Any())
                    {
                        img = site_abit_url + im.First().Descendants("img").First()
                            .GetAttributeValue("src", "");
                    }
                    else
                    {
                        var imf = txt.Descendants("img");
                        if (imf.Any())
                        {
                            img = site_abit_url + imf.First().GetAttributeValue("src", "");
                        }
                    }
                    var text = txt.OuterHtml;
                    title = WebUtility.HtmlDecode(title).Replace('\n', ' ').Replace('\t', ' ').Trim(' ');
                    date = WebUtility.HtmlDecode(date).Replace('\n', ' ').Replace('\t', ' ').Trim(' ');

                    items.Add(new NewsItem()
                    {
                        Url = url,
                        Title = title,
                        Date = date,
                        Detailed = new NewsItem.NewsItemDetailed() { ContentHTML = text },
                        ImageURL = img
                    });
                }
                catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }

                return items;
            }

            public static void PageThread(object item)
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
                        {
                            continue;
                        }
                    }
                    CurrentPageNews.Add(new NewsItem()
                    {
                        Excerpt = WebUtility.HtmlDecode(desc_text),
                        ImageURL = img,
                        Title = title,
                        Date = date,
                        Url = read
                        // PageId = articles.Item2
                    });
                }
                return CurrentPageNews;
            }
            public static async Task SaveCache(string key = "")
            {
                if (Current != null)
                {
                    if (key == "")
                    {
                        foreach (var ParserX in Current.POOL.Keys)
                        {
                            await Saver(ParserX);
                        }

                        SaveInstantCache();
                    }
                    else
                    {
                        await Saver(key);
                    }
                }
            }
            private static async Task Saver(string ParserX)
            {
                var ig = Current.POOL[ParserX];
                if (ig.newslist != null && ig.newslist.Count > 0)
                { 

                    var toper = File.CreateText(Path.Combine(
                    MainApp.GetApp.Server.DirectoryManager[MainApp.Dirs.Cache], "news_" + ParserX + ".txt"));
                    await toper.WriteAsync(JsonConvert.SerializeObject(ig.newslist));
                    toper.Close();
                }
            }
            public class PoolParserScheduler
            {
                public PoolParserScheduler(string par)
                {
                    ParserX = par;
                }

                private string ParserX { get; set; }

                private System.Timers.Timer timer;
                private int id = -100, offset;
                public async Task ReparseTask()
                {
                    if ( Tools.CheckForInternetConnection())
                    {
                        await Saver(ParserX);
                        var t = Current.POOL[ParserX];
                        offset = t.offsetMins;
                        var p = new Parser(t.url, ParserX, t.abitParser, t.InstituteID, t.offsetMins) { CacheEpoch = ++t.CacheEpoch };
                        await p.ParsePages(t);
                        Current.POOL[ParserX] = p;
                        t.CacheEpoch++;
                    }
                    else
                    {
                        Schedule_Timer(ParserX);
                    }
                }

                private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
                {
                    timer.Stop();
                    await ReparseTask();
                }
                public DateTime scheduledTime;
                public void Schedule_Timer(string xkey, TimeSpan timeToSleep = new TimeSpan(), int Offset = 0, int ID = 0)
                {
                    if (ID != 0)
                    {
                        id = ID;
                    }

                    DateTime nowTime = TimeChron.GetRealTime();

                    if (timeToSleep == new TimeSpan())
                    {
                        int delayMins = 0, delayHours = 0;
                        if (id != -100)
                        {
                            delayHours = MainApp.taskDelayH;
                        }
                        else
                        {
                            delayMins = MainApp.taskDelayM;
                        }

                        timeToSleep = new TimeSpan(delayHours, delayMins, 0);
                    }
                    try
                    {
                        scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, 0, 0).
                            AddHours(timeToSleep.Hours).AddMinutes(timeToSleep.Minutes +
                            ((Current.POOL[xkey].CacheEpoch == 0) ? Offset : 0));
                    }
                    catch { Console.WriteLine("error: " + xkey); return; }

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
            private List<NewsItem> newslist;
            public List<NewsItem> Newslist
            {
                get => newslist ?? (newslist = new List<NewsItem>());
                set {
                    if (value != null)
                    {
                        newslist = value;
                    }
                }
            }
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
            if (Detailed == null)
            {
                return "wait for minute";
            }

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
                try
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
                        item.Detailed.DocsLinks = new List<DocItem>();
                        foreach (var doc in docs)
                        {
                            var f = doc.NextSibling;
                            var t = f.ChildNodes[0].GetAttributeValue("data-href", "");
                            var type = f.ChildNodes[0].GetClasses().Where(x => x != "img" && x != "ib").First();
                            item.Detailed.DocsLinks.Add(new DocItem(doc.InnerText, @"" + WebUtility.UrlEncode(t), type));
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
                        {
                            item.RelUrl = WebUtility.UrlEncode(site_url + "/" + y.Last().Attributes["href"].Value);
                        }
                    }
                    #endregion

                    #region Text
                    var text = artc.Descendants().Where(x => x.HasAttributes && x.GetAttributeValue("id", "").Contains("item-desc")).First();
                    var xr = text.Descendants().Where(x => (x.InnerHtml.Contains("id=\"gallery") || x.HasClass("back") || x.HasAttributes && x.GetAttributeValue("role", "") == "photo"));
                    if (xr.Any())
                    {
                        for (var i = 0; i < xr.Count(); i++)
                        {
                            if (text.ChildNodes.Contains(xr.ElementAt(i)))
                            {
                                text.RemoveChild(xr.ElementAt(i));
                            }
                        }
                    }
                    if (docs != null && docs.Count() > 0)
                    {
                        var v = docs.First().ParentNode;
                        if (v.Name == "tr")
                        {
                            v.ParentNode.Remove();
                        }
                        else
                        {
                            v.RemoveAllChildren();
                        }
                    }

                    item.Detailed.ContentHTML = @"" + (text.OuterHtml.Replace("%22", "%5C%22"));


                }
                catch (Exception ex)
                {
                    MainApp.GetApp.Server.Logger.NotifyError(ex);
                }
                #endregion
            }

        }
    }
}
