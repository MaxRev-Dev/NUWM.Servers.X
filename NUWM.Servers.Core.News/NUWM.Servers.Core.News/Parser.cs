using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using MaxRev.Utils.Schedulers;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.News
{
    public class ScheduleInstantCacheSave : BaseScheduler
    {
        private readonly ParserPool pool;

        public ScheduleInstantCacheSave(ParserPool pool) : base(TimeSpan.FromHours(1))
        {
            this.pool = pool;
        }

        protected override void OnTimerElapsed()
        {
            var g = File.CreateText(pool.InstP);
            g.WriteAsync(JsonConvert.SerializeObject(pool.InstantCache)).Wait();
            g.Close();
        }
    }
    public class Parser
    {
        private readonly string url;
        private bool abitParser;
        public string xkey;
        public int pagesDef = 15;
        private readonly int offsetMins;
        private HtmlDocument CurrentDoc;
        public readonly int InstituteID;
        public int CacheEpoch { get; set; }
        public PoolParserScheduler scheduler;

        public Parser(string url, string key, bool abitParser, int institute_id = -100, int offset = 0)
        {
            xkey = key;
            offsetMins = offset;
            this.abitParser = abitParser;
            InstituteID = institute_id;
            this.url = url;
            NewsList = new List<NewsItem>();
        }

        public string site_url => App.Get.Config.HostUrl;

        public async void ParsePagesParallel(Parser parser_obj)
        {
            await ParsePages(parser_obj);
        }

        public async Task ParsePages(Parser parser)
        {
            parser.scheduler = null;
            if (!abitParser)
            {
                try
                {
                    var urlx = parser.url;
                    CurrentDoc = new HtmlDocument();
                    var rm = await RequestAllocator.Instance.UsingPool(new Request(urlx));

                    if (rm.IsSuccessStatusCode)
                    {
                        CurrentDoc.LoadHtml(await rm.Content.ReadAsStringAsync());

                        var news_art = CurrentDoc.DocumentNode.Descendants().Single(x => x.HasClass("news") && x.HasClass("list"));
                        if (NewsList == null)
                        {
                            NewsList = new List<NewsItem>();
                        }

                        var op = ParseInstance(new Tuple<HtmlNode, int, string>(news_art, 1, url));
                        if (NewsList.Count > 0)
                        {
                            NewsList.AddRange(op.Where(x => NewsList.All(y => x.Url != y.Url)));
                        }
                        else
                        {
                            NewsList.AddRange(op);
                        }

                        foreach (var t in op.Where(x => x.Detailed == null || x.Detailed == new NewsItemDetailed()))
                        {
                            await PageThread(t);
                        }

                        int pages_count = Convert.ToInt16(news_art.NextSibling.ChildNodes[news_art.NextSibling.ChildNodes.Count - 4].InnerText);
                        pagesDef = pages_count < App.Get.Config.PagesDefault
                            ? pages_count
                            : App.Get.Config.PagesDefault;

                        for (int id = 2; id < pagesDef + 1; id++)
                        {
                            var rmx = await RequestAllocator.Instance.UsingPool(new Request(string.Format(urlx + "?p={0}", id)));
                            if (rmx.IsSuccessStatusCode)
                            {
                                var str = await rmx.Content.ReadAsStringAsync();

                                CurrentDoc = new HtmlDocument();
                                CurrentDoc.LoadHtml(str);

                                news_art = CurrentDoc.DocumentNode.Descendants().Single(x => x.HasClass("news") && x.HasClass("list"));
                                var items = ParseInstance(new Tuple<HtmlNode, int, string>(news_art, id, url));

                                NewsList.AddRange(items.Where(x => NewsList.All(y => x.Url != y.Url)));

                                foreach (var it in items)
                                {
                                    await PageThread(it);
                                }
                            }
                        }
                        try
                        {
                            NewsList = NewsList.OrderByDescending(x => DateTime.ParseExact(x.Date, "dd MMMM yyyy", CultureInfo.CreateSpecificCulture("uk-UA"))).ToList();
                        }
                        catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }
                    }
                    //#endif
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            else
            {
                // ABIT PAGES
                NewsList.AddRange(await AbitNewsParser(parser));
                try
                {

                    //newslist = newslist.OrderByDescending(x => 
                    //DateTime.Parse(x.Date.Substring(x.Date.IndexOf(',')).Trim(), new CultureInfo("uk-UA"))
                    //   ).ToList();
                }
                catch (Exception ex)
                {
                    App.Get.Core.Logger.NotifyError(LogArea.Other, ex);
                }
            }

            // Fix of server connection problems)
            try
            {
                if (NewsList != null && NewsList.Count == 0)
                {
                    NewsList = DeepCopy(parser.NewsList);
                }
            }
            catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }

            if (scheduler == null)
            {
                scheduler = new PoolParserScheduler(ParserPool.Current, xkey);
                //scheduler.ScheduleTimer(xkey, new TimeSpan(), offsetMins, InstituteID);
            }
        }


        private async Task<List<NewsItem>> AbitNewsParser(Parser parser)
        {
            List<NewsItem> items = new List<NewsItem>();
            try
            {
                var r = await RequestAllocator.Instance.UsingPool(new Request(parser.url));
                if (r.IsSuccessStatusCode)
                {
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(await r.Content.ReadAsStringAsync());
                    var it = doc.DocumentNode.Descendants("div").Where(x => x.HasClass("pagination")).ToArray();
                    if (it.Any())
                    {
                        var its = it.First();
                        foreach (var i in its.Descendants("a").Where(x => x.GetAttributeValue("class", "") == ""))
                        {
                            var link = site_abit_url + i.GetAttributeValue("href", "");
                            var v = await RequestAllocator.Instance.UsingPool(new Request(link));
                            if (v.IsSuccessStatusCode)
                            {
                                HtmlDocument docx = new HtmlDocument();
                                docx.LoadHtml(await v.Content.ReadAsStringAsync());
                                items.AddRange(await AbitPageItems(docx));
                            }
                            else
                            {
                                App.Get.Core.Logger.NotifyError(LogArea.Other, new Exception($"Failed to get { link}"));
                            }
                        }
                    }
                    // Also parsing current first page
                    items.AddRange(await AbitPageItems(doc));
                }
                else
                {
                    App.Get.Core.Logger.NotifyError(LogArea.Other, new Exception($"Failed to get { parser.url}"));
                }
            }
            catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }
            return items;
        }

        public string site_abit_url => App.Get.Config.AbitUrl;

        private async Task<List<NewsItem>> AbitPageItems(HtmlDocument docx)
        {
            List<NewsItem> items = new List<NewsItem>();
            try
            {
                foreach (var ids in docx.GetElementbyId("k2Container").Descendants().Where(x => x.HasClass("catItemTitle")))
                {
                    var itemUrl = site_abit_url + ids.Element("a").GetAttributeValue("href", "");
                    var v1 = await RequestAllocator.Instance.UsingPool(new Request(itemUrl));
                    if (v1.IsSuccessStatusCode)
                    {
                        HtmlDocument docx1 = new HtmlDocument();
                        docx1.LoadHtml(await v1.Content.ReadAsStringAsync());
                        items.AddRange(AbitPage(docx1, itemUrl));
                    }
                    else
                    {
                        App.Get.Core.Logger.NotifyError(LogArea.Other, new Exception($"Failed to get {itemUrl}"));
                    }
                }
            }
            catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }
            return items;
        }
        private List<NewsItem> AbitPage(HtmlDocument doc, string itemUrl)
        {
            List<NewsItem> items = new List<NewsItem>();
            try
            {
                var i = doc.GetElementbyId("k2Container");
                var txt = i.Descendants().First(x => x.HasClass("itemFullText"));
                var title = i.Descendants().First(x => x.HasClass("itemTitle")).InnerHtml;
                var date = i.Descendants().First(x => x.HasClass("itemDateCreated")).InnerHtml;
                var im = i.Descendants().Where(x => x.HasClass("itemImage")).ToArray();
                string img = default;
                if (im.Any())
                {
                    img = site_abit_url + im.First().Descendants("img").First()
                              .GetAttributeValue("src", "");
                }
                else
                {
                    var imf = txt.Descendants("img").ToArray();
                    if (imf.Any())
                    {
                        img = site_abit_url + imf.First().GetAttributeValue("src", "");
                    }
                }
                var text = txt.OuterHtml;
                if (title != default)
                    title = WebUtility.HtmlDecode(title).Replace('\n', ' ').Replace('\t', ' ').Trim(' ');
                if (date != default)
                    date = WebUtility.HtmlDecode(date).Replace('\n', ' ').Replace('\t', ' ').Trim(' ');

                items.Add(new NewsItem
                {
                    Url = itemUrl,
                    Title = title,
                    Date = date,
                    Detailed = new NewsItemDetailed { ContentHTML = text },
                    ImageURL = img
                });
            }
            catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }

            return items;
        }

        public static Task PageThread(NewsItem item)
        {
            return NewsItemDetailed.Process(item);
        }

        public static List<NewsItem> ParseInstance(Tuple<HtmlNode, int, string> articles)
        {
            List<NewsItem> CurrentPageNews = new List<NewsItem>();
            foreach (var i in articles.Item1.ChildNodes)
            {
                var wrimg = i.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                var img = App.Get.Config.HostUrl + wrimg.Attributes["src"].Value;
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
                CurrentPageNews.Add(new NewsItem
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
        public class PoolParserScheduler : BaseScheduler
        {
            public PoolParserScheduler(ParserPool pool, string par)
            {
                Pool = pool;
                ParserX = par;
            }

            public ParserPool Pool { get; }
            private string ParserX { get; }

            //private int id = -100, offset;
            public async Task ReparseTask()
            {
                if (Tools.CheckForInternetConnection())
                {
                    await Pool.Saver(ParserX);
                    var t = Pool.POOL[ParserX];
                    //offset = t.offsetMins;
                    var p = new Parser(t.url, ParserX, t.abitParser, t.InstituteID, t.offsetMins) { CacheEpoch = ++t.CacheEpoch };
                    await p.ParsePages(t);
                    Pool.POOL[ParserX] = p;
                    t.CacheEpoch++;
                }
                else
                {
                    ScheduleTimer();
                }
            }

            protected override async void OnTimerElapsed()
            {
                await ReparseTask();
            }
            //public void Schedule_Timer(string xkey, TimeSpan timeToSleep = new TimeSpan(), int Offset = 0, int ID = 0)
            //{
            //    if (ID != 0)
            //    {
            //        id = ID;
            //    }

            //    DateTime nowTime = TimeChron.GetRealTime();

            //    if (timeToSleep == new TimeSpan())
            //    {
            //        int delayMins = 0, delayHours = 0;
            //        if (id != -100)
            //        {
            //            delayHours = MainApp.taskDelayH;
            //        }
            //        else
            //        {
            //            delayMins = MainApp.taskDelayM;
            //        }

            //        timeToSleep = new TimeSpan(delayHours, delayMins, 0);
            //    }
            //    try
            //    {
            //        scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, 0, 0).
            //            AddHours(timeToSleep.Hours).AddMinutes(timeToSleep.Minutes +
            //            ((Current.POOL[xkey].CacheEpoch == 0) ? Offset : 0));
            //    }
            //    catch { Console.WriteLine("error: " + xkey); return; }

            //    if (nowTime > scheduledTime)
            //    {
            //        scheduledTime = scheduledTime.AddHours(12);
            //    }

            //    double tickTime = (scheduledTime - TimeChron.GetRealTime()).TotalMilliseconds;
            //    timer = new Timer(tickTime);
            //    timer.Elapsed += Timer_Elapsed;
            //    timer.Start();
            //}
        }

        [field: NonSerialized] public List<NewsItem> NewsList;
        public List<NewsItem> Newslist
        {
            get => NewsList ?? (NewsList = new List<NewsItem>());
            set {
                if (value != null)
                {
                    NewsList = value;
                }
            }
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