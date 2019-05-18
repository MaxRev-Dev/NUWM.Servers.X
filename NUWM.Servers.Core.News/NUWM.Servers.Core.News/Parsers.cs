using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.News
{
    public class ParserPool : IDisposable
    {
        public static ParserPool Current;
        private ConcurrentDictionary<string, Parser> ppoll;
        private ExpireCacheUpdater updater;

        public ParserPool()
        {
            updater = new ExpireCacheUpdater();
        }

        public ConcurrentDictionary<string, Parser> POOL => ppoll ?? (ppoll = new ConcurrentDictionary<string, Parser>());
        public void SetCurrent(ParserPool u = null)
        {
            Current = u ?? this;
        }

        #region InstantCacheImpl
        public List<NewsItem> InstantCache = new List<NewsItem>();
        public void SaveInstantCache()
        {
            try
            {
                if (InstantCache != null && InstantCache.Count > 0)
                {
                    File.WriteAllTextAsync(InstP, JsonConvert.SerializeObject(InstantCache));
                }
            }
            catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }
        }
        public readonly string InstP = Path.Combine(
            App.Get.Core.DirectoryManager[App.Dirs.Cache], "instantCache.txt");
        public async void LoadInstantCache()
        {
            if (File.Exists(InstP))
            {
                try
                {
                    InstantCache = JsonConvert.DeserializeObject<List<NewsItem>>
                        (await File.OpenText(InstP).ReadToEndAsync());
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        public async Task<Tuple<NewsItem, InstantState>> ParsePageInstant(string url, bool html)
        {
            try
            {

                if (InstantCache == null)
                {
                    InstantCache = new List<NewsItem>();
                }

                InstantState state = InstantState.FromCache;
                if (InstantCache != null && InstantCache.Count > 0)
                {
                    var spl = url.Substring(url.LastIndexOf('/'));
                    if (url.EndsWith('/'))
                    {
                        // url.Substring(0, url.Length - 2);
                    }

                    NewsItem obj;
                    foreach (var i in POOL.Values)
                    {
                        var u = i.NewsList.Where(x => x.Url.Contains(spl)).ToArray();
                        if (u.Count() == 1)
                        {
                            obj = Parser.DeepCopy(u.First());
                            obj.Detailed.ContentHTML = html ? u.First().Detailed.ContentHTML : u.First().GetText();

                            return new Tuple<NewsItem, InstantState>(obj, state);
                        }
                    }
                    var op = InstantCache.Where(x => x.Url.Contains(spl)).ToArray();
                    if (op.Count() == 1)
                    {
                        obj = Parser.DeepCopy(op.First());
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
                HttpResponseMessage rm = await RequestAllocator.Instance.UsingPool(new Request(url));
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
                    item.ImageURL = App.Get.Config.HostUrl +
                                    imgp.Substring(1, imgp.Length - 1);
                    item.Url = url;
                    NewsItemDetailed.ParseArticle(item, doc.DocumentNode.Descendants().First(x => x.Name == "article" && x.HasClass("item-detailed")));
                    InstantCache.Add(item);

                    var ret = Parser.DeepCopy(item);
                    ret.Detailed.ContentHTML = html ? ret.Detailed.ContentHTML : ret.GetText();
                    return new Tuple<NewsItem, InstantState>(ret, InstantState.Success);
                }

                return new Tuple<NewsItem, InstantState>(null, InstantState.TimedOut);
            }
            catch (Exception)
            {
                return new Tuple<NewsItem, InstantState>(null, InstantState.ErrorParsing);
            }

        }


        #endregion
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
            var file = Path.Combine(App.Get.Core.DirectoryManager[App.Dirs.AddonsNews], "urls.txt");
            while (!File.Exists(file))
            {
                Console.WriteLine("Urls missing!");
                await Task.Delay(5000);
            }

            string direct;
            using (StreamReader f = File.OpenText(file))
            {
                direct = await f.ReadToEndAsync();
            }

            string[] lines = direct.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            //int GetOptionOrDefault(string name, int defaultVal)
            //{
            //    if (direct.Contains(name))
            //        return int.Parse(new Regex($@"(?<={name}\:)[0-9]*").Match(direct).Groups[0].Value);
            //    return defaultVal;
            //}
            //App.OffsetLen = GetOptionOrDefault("offsetLen", 5);
            //App.Get.Config.TaskDelayMinutes = GetOptionOrDefault("delayM", 5);
            //App.Get.Config.TaskDelayHours = GetOptionOrDefault("delayH", 1);
            //App.Get.Config.CacheAlive = GetOptionOrDefault("CacheAliveHours", 12);
            //App.Get.Config.PagesDefault = GetOptionOrDefault("default_pages_count", 15);
            return lines;
        }
        public async Task Run()
        {
            string[] lines = await InitSettings();
            int offset = -App.OffsetLen;
            int newsOffset = -App.Get.Config.TaskDelayMinutes;
            Current.LoadInstantCache();
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

                    if (key != null && key.Equals("nuwm.edu.ua"))
                    {
                        key = string.Join("", strs[0].Substring(strs[0].LastIndexOf('/') + 1).Split('-', '_').Select(x => x[0].ToString()));
                    }

                    if (key != null && key.Contains("zaochno-distanc"))
                    {
                        key = "zdn";
                    }
                    new Task(async () =>
                    {
                        Parser parser = new Parser(news_url, key, abit, unid,
                                unid == -100 ? newsOffset += App.Get.Config.TaskDelayMinutes : offset += App.OffsetLen)
                        { CacheEpoch = 0 };
                        try
                        {
                            if (!POOL.TryAdd(key, parser))
                            {
                                await LoadNewsCache(key, parser);
                                POOL[key] = parser;
                            }
                        }
                        catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }
                        finally
                        {
                            if (parser.Newslist.Count == 0)
                            {
                                await LoadNewsCache(key, parser);
                            }

                            RunParseThread(parser);
                        }
                    }).Start();
                }
            }
            catch (Exception ex)
            {
                App.Get.Core.Logger.NotifyError(LogArea.Other, ex);
            }
        }
        private void RunParseThread(Parser parser)
        {
            try
            {
                Task.Run(() => parser.ParsePagesParallel(parser));
            }
            catch
            {
                // ignored
            }
        }

        private async Task LoadNewsCache(string key, Parser parser)
        {
            try
            {
                var f = Path.Combine(App.Get.Core.DirectoryManager[App.Dirs.Cache], "news_" + key + ".txt");
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
            catch (Exception ex) { App.Get.Core.Logger.NotifyError(LogArea.Other, ex); }
        }


        public async Task SaveCache(string key = "")
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

        public async Task Saver(string ParserX)
        {
            var ig = POOL[ParserX];
            if (ig.NewsList != null && ig.NewsList.Count > 0)
            {

                var toper = File.CreateText(Path.Combine(
                    App.Get.Core.DirectoryManager[App.Dirs.Cache], "news_" + ParserX + ".txt"));
                await toper.WriteAsync(JsonConvert.SerializeObject(ig.NewsList));
                toper.Close();
            }
        }

        public void Dispose()
        {
            updater.StopTimer(); 
        }
    }
}
