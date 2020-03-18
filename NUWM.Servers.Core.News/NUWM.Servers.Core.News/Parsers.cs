using HtmlAgilityPack;
using JSON;
using Lead;
using MaxRev.Servers.Utils;
using Newtonsoft.Json;
using NUWM.Servers.Core.News;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MaxRev.Utils;
using MaxRev.Utils.Schedulers;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils.Logging;

namespace Lead
{
    [Serializable]
    public class InstantCacheSaveScheduler : BaseScheduler
    {
        private readonly InstantCacher _cacher;
        public InstantCacheSaveScheduler(InstantCacher cacher)
        {
            _cacher = cacher;
            CurrentWorkHandler = SaveInstantCache;
        }

        /// <exception cref="T:System.UnauthorizedAccessException">The caller does not have the required permission.</exception>
        public async void SaveInstantCache()
        {
            using (var g = File.CreateText(_cacher.InstantCachePath))
                await g.WriteAsync(JsonConvert.SerializeObject(_cacher.InstantCacheList)).ConfigureAwait(false);
        }
    }
    [Serializable]
    class AbitNewsParser : Parser
    {
        public AbitNewsParser(ILogger logger) : base(logger)
        {
        }
        private async Task<List<NewsItem>> AbitNewsParserAsync(string parser_url)
        {
            List<NewsItem> items = new List<NewsItem>();
            try
            {
                using (var request = new Request(parser_url))
                {
                    var r = await RequestAllocator.Instance.UsingPoolAsync(request).ConfigureAwait(false);
                    if (r.IsSuccessStatusCode)
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
                        var it = doc.DocumentNode.Descendants("div").Where(x => x.HasClass("pagination")).ToArray();
                        if (it.Any())
                        {
                            var its = it.First();
                            foreach (var i in its.Descendants("a").Where(x => x.GetAttributeValue("class", "") == ""))
                            {
                                var link = ParserPool.site_abit_url + i.GetAttributeValue("href", "");
                                using (var req = new Request(link))
                                {
                                    var v = await RequestAllocator.Instance.UsingPoolAsync(req).ConfigureAwait(false);
                                    if (v.IsSuccessStatusCode)
                                    {
                                        HtmlDocument docx = new HtmlDocument();
                                        docx.LoadHtml(await v.Content.ReadAsStringAsync().ConfigureAwait(false));
                                        items.AddRange(await AbitPageItemsAsync(docx).ConfigureAwait(false));
                                    }
                                    else
                                    {
                                        MainApp.GetApp.Core.Logger.NotifyError(LogArea.Other,
                                            new Exception($"Failed to get {link}"));
                                    }
                                }
                            }
                        }

                        // Also parsing current first page
                        items.AddRange(await AbitPageItemsAsync(doc).ConfigureAwait(false));
                    }
                    else
                    {
                        _logger.NotifyError(LogArea.Other,
                             new Exception($"Failed to get {parser_url}"));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.NotifyError(LogArea.Other, ex);
            }
            return items;
        }

        private async Task<List<NewsItem>> AbitPageItemsAsync(HtmlDocument docx)
        {
            var items = new List<NewsItem>();
            try
            {
                foreach (var ids in docx.GetElementbyId("k2Container").Descendants().Where(x => x.HasClass("catItemTitle")))
                {
                    var lurl = ParserPool.site_abit_url + ids.Element("a").GetAttributeValue("href", "");
                    using (var r = new Request(lurl))
                    {
                        var v1 = await RequestAllocator.Instance.UsingPoolAsync(r).ConfigureAwait(false);
                        if (v1.IsSuccessStatusCode)
                        {
                            HtmlDocument docx1 = new HtmlDocument();
                            docx1.LoadHtml(await v1.Content.ReadAsStringAsync().ConfigureAwait(false));
                            items.AddRange(AbitPage(docx1, lurl));
                        }
                        else
                        {
                            MainApp.GetApp.Core.Logger.NotifyError(LogArea.Other, new Exception($"Failed to get {lurl}"));
                        }
                    }
                }
            }
            catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(LogArea.Other, ex); }
            return items;
        }

        private List<NewsItem> AbitPage(HtmlDocument doc, string lurl)
        {
            var items = new List<NewsItem>();
            try
            {
                var i = doc.GetElementbyId("k2Container");
                var txt = i.Descendants().First(x => x.HasClass("itemFullText"));
                var title = i.Descendants().First(x => x.HasClass("itemTitle")).InnerHtml;
                var date = i.Descendants().First(x => x.HasClass("itemDateCreated")).InnerHtml;
                var im = i.Descendants().Where(x => x.HasClass("itemImage")).ToArray();
                string img = null;
                if (im.Any())
                {
                    img = ParserPool.site_abit_url + im.First().Descendants("img").First()
                              .GetAttributeValue("src", "");
                }
                else
                {
                    var imf = txt.Descendants("img").ToArray();
                    if (imf.Any())
                    {
                        img = ParserPool.site_abit_url + imf.First().GetAttributeValue("src", "");
                    }
                }
                var text = txt.OuterHtml;
                title = WebUtility.HtmlDecode(title)?.Replace('\n', ' ').Replace('\t', ' ').Trim(' ');
                date = WebUtility.HtmlDecode(date)?.Replace('\n', ' ').Replace('\t', ' ').Trim(' ');

                var item = new NewsItem(lurl, title, date, img)
                {
                    Url = lurl,
                    Title = title,
                    Date = date,
                    Detailed = new NewsItem.NewsItemDetailed(),
                    ImageURL = img
                };
                item.Detailed.ContentHTML = @"" + (text.Replace("%22", "%5C%22"));
                items.Add(item);
            }
            catch (Exception ex) { _logger.NotifyError(LogArea.Other, ex); }


            return items;
        }

        public override async Task ParsePagesAsync(string parser_url)
        {
            newslist.AddRange(await AbitNewsParserAsync(parser_url).ConfigureAwait(false));
            try
            {

                //newslist = newslist.OrderByDescending(x => 
                //DateTime.Parse(x.Date.Substring(x.Date.IndexOf(',')).Trim(), new CultureInfo("uk-UA"))
                //   ).ToList();
            }
            catch (Exception ex)
            {
                _logger.NotifyError(LogArea.Other, ex);
            }
        }

    }

    [Serializable]
    public abstract class Parser : IDisposable
    {
        protected readonly ILogger _logger;

        internal string Url { get; private set; }
        public string Key { get; private set; }
        public int InstituteID { get; private set; }

        protected List<NewsItem> newslist = new List<NewsItem>();
        public int CacheEpoch { get; set; }

        public Parser(ILogger logger)
        { 
            _logger = logger;
        }
        public Parser FromParams(string url, string key, int institute_id = -100)
        {
            Key = key;
            InstituteID = institute_id;
            Url = url;
            return this;
        }


        public abstract Task ParsePagesAsync(string parser_url);
        public List<NewsItem> Newslist
        {
            get => newslist ?? (newslist = new List<NewsItem>());
            set {
                if (value != null)
                {
                    newslist.Clear();
                    newslist = value;
                }
            }
        }

        /// <exception cref="T:System.Runtime.Serialization.SerializationException">An error has occurred during serialization, such as if an object in the <paramref>graph</paramref> parameter is not marked as serializable.</exception>
        /// <exception cref="T:System.Security.SecurityException">The caller does not have the required permission.</exception>
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

        public void Dispose()
        {
            newslist.Clear();
        }
    }
    [Serializable]
    class NewsParser : Parser
    {
        public NewsParser(ILogger logger) : base(logger)
        {
        }

        public override async Task ParsePagesAsync(string parser_url)
        {

            try
            {
                using (var r = new Request(parser_url))
                {
                    var rm = await RequestAllocator.Instance.UsingPoolAsync(r).ConfigureAwait(false);

                    if (rm.IsSuccessStatusCode)
                    {
                        var CurrentDoc = new HtmlDocument();
                        CurrentDoc.LoadHtml(await rm.Content.ReadAsStringAsync().ConfigureAwait(false));

                        var news_art = CurrentDoc.DocumentNode.Descendants().Single(x => x.HasClass("news") && x.HasClass("list"));
                        if (newslist == null)
                        {
                            newslist = new List<NewsItem>();
                        }

                        var op = ParseInstance(new Tuple<HtmlNode, int, string>(news_art, 1, Url)).ToArray();

                        newslist.AddRange(newslist.Count > 0 ? op.Where(x => newslist.All(y => x.Url != y.Url)) : op);

                        var tasks = op.Where(x => x.Detailed == null || x.Detailed == new NewsItem.NewsItemDetailed())
                             .Select(x => x.FetchAsync()).Cast<Task>().ToArray();
                        Task.WaitAll(tasks);

                        int pages_count = Convert.ToInt16(news_art.NextSibling.ChildNodes[news_art.NextSibling.ChildNodes.Count - 4].InnerText);
                        var pagesDef = pages_count < MainApp.Config.DefaultPagesCount ? pages_count : MainApp.Config.DefaultPagesCount;

                        for (int id = 2; id < pagesDef + 1; id++)
                        {
                            using (var request = new Request(string.Format(parser_url + "?p={0}", id)))
                            {
                                var rmx = await RequestAllocator.Instance.UsingPoolAsync(request).ConfigureAwait(false);
                                if (rmx.IsSuccessStatusCode)
                                {
                                    var str = await rmx.Content.ReadAsStringAsync().ConfigureAwait(false);

                                    CurrentDoc = new HtmlDocument();
                                    CurrentDoc.LoadHtml(str);

                                    news_art = CurrentDoc.DocumentNode.Descendants().Single(x => x.HasClass("news") && x.HasClass("list"));
                                    var items = ParseInstance(new Tuple<HtmlNode, int, string>(news_art, id, Url)).ToArray();

                                    newslist.AddRange(items.Where(x => newslist.All(y => x.Url != y.Url)));

                                    Task.WaitAll(items.Select(x => x.FetchAsync()).Cast<Task>().ToArray());

                                }
                            }
                        }

                        try
                        {
                            var cu = CultureInfo.CreateSpecificCulture("uk-UA");
                            var t = newslist.First().Date;
                            if (DateTime.TryParseExact(t, "dd MMMM yyyy", cu, DateTimeStyles.None, out _))
                            {
                                newslist = newslist.OrderByDescending(x =>
                                    DateTime.ParseExact(x.Date, "dd MMMM yyyy", cu)).ToList();
                            }
                            else
                            {
                                newslist = newslist.OrderByDescending(x => DateTime.Parse(x.Date, cu)).ToList();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.NotifyError(LogArea.Other, ex);
                        }
                    }
                    //#endif
                }
            }
            catch (Exception)
            {
                // ignored
            }



            //// Fix of server connection problems)
            //try
            //{
            //    if (newslist.Count == 0)
            //    {
            //        newslist = DeepCopy(parser.newslist);
            //    }
            //}
            //catch (Exception ex) { MainApp.GetApp.Server.Logger.NotifyError(ex); }

        }

        private IEnumerable<NewsItem> ParseInstance(Tuple<HtmlNode, int, string> articles)
        {
            foreach (var i in articles.Item1.ChildNodes)
            {
                var wrimg = i.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                var img = ParserPool.site_url + wrimg.Attributes["src"].Value;
                var origImg = i.OwnerDocument.GetElementbyId("originalHeaderImage");
                img = WebUtility.HtmlDecode(origImg?.InnerText.Trim('\"') ?? img);
                var desc = i.ChildNodes[1].ChildNodes;

                var date = desc[0].InnerText;
                var title = desc[1].InnerText;
                var desc_text = desc[2].InnerText;
                var read = desc[3].ChildNodes[0].Attributes["href"].Value;
                var lurl = articles.Item3;
                if (read.Contains(lurl))
                {
                    var t = read.Split(lurl, StringSplitOptions.RemoveEmptyEntries).First();
                    if (t.Length == 1)
                    {
                        continue;
                    }
                }
                yield return new NewsItem(read, title, date, img)
                {
                    Excerpt = WebUtility.HtmlDecode(desc_text),
                };
            }
        }

    }
}

namespace JSON
{
    public class NewsItemVisualizer
    {
        [JsonProperty("item")]
        public List<NewsItem> NewsItemList { get; set; }
    }
    public partial class NewsItem
    {
        public NewsItem(string url, string title, string date, string img)
        {
            Url = url;
            Title = title;
            Date = date;
            ImageURL = img;
        }

        public string GetText()
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(NewsConstants.HtmlBoilerMinimal);
            if (Detailed == null)
            {
                return "wait for minute";
            }

            HtmlNode node = Detailed?.ContentHTML != default ? new HtmlNode(HtmlNodeType.Element, doc, 0)
            {
                InnerHtml = Detailed.ContentHTML
            } : default;

            return node?.InnerText ?? "EMPTY";
        }
        private async Task ProcessAsync()
        {
            using (var r = new Request(Url))
            {
                var mess = await RequestAllocator.Instance.UsingPoolAsync(r).ConfigureAwait(false);

                HtmlDocument doc = new HtmlDocument();
                if (mess.IsSuccessStatusCode)
                {
                    doc.LoadHtml(mess.Content.ReadAsStringAsync().Result);
                    ParseArticle(this, doc.DocumentNode.Descendants().FirstOrDefault(x => x.Name == "article" && x.HasClass("item-detailed")));
                }
            }
        }

        public static void ParseArticle(NewsItem cache, HtmlNode article)
        {
            try
            {
                cache.Detailed = new NewsItemDetailed();
                if (article == default)
                    return;
                if (string.IsNullOrEmpty(cache.Date))
                {
                    cache.Date = article.Descendants("time").First().InnerText;
                    var img = article.Descendants("img").First().GetAttributeValue("src", "");

                    cache.TryUpdateImageURL(ParserPool.site_url + img.Substring(1));
                }
                var origImg = article.OwnerDocument.GetElementbyId("originalHeaderImage");
                cache.TryUpdateImageURL(WebUtility.HtmlDecode(origImg?.InnerText.Trim('\"')));

                #region Docs
                var docs = article.Descendants().Where(x => x.HasAttributes && x.GetAttributeValue("class", "null").Contains("file-desc")).ToArray();
                if (docs.Any())
                {
                    cache.Detailed.DocsLinks = new List<DocItem>();
                    foreach (var doc in docs)
                    {
                        var f = doc.NextSibling;
                        var t = f.ChildNodes[0].GetAttributeValue("data-href", "");
                        var type = f.ChildNodes[0].GetClasses().First(x => x != "img" && x != "ib");
                        cache.Detailed.DocsLinks.Add(new DocItem(doc.InnerText, @"" + WebUtility.UrlEncode(t), type));
                    }
                }
                #endregion


                #region Images
                var imgnode = article.Descendants().Where(x =>
                x.Name == "div" && x.HasClass("s1") && x.GetAttributeValue("role", "") == "marquee").ToArray();

                if (imgnode.Any())
                {
                    cache.Detailed.ImagesLinks = new List<string>();
                    foreach (var y in imgnode)
                    {
                        var box = y.ChildNodes.Single(x => x.HasClass("box"));
                        foreach (var i in box.FirstChild.ChildNodes)
                        {
                            string uri = i.FirstChild.Attributes["src"].Value;
                            uri = uri.Replace(new Regex(@"(?<=photo.).*(?=\/)").Match(uri).Value, "0");
                            cache.Detailed.ImagesLinks.Add(ParserPool.site_url + uri.Substring(1));
                        }
                    }
                }
                #endregion


                #region RelatedLink
                var rel = article.Descendants().Where(x => x.InnerText.Contains("Читайте також")).ToArray();
                if (rel.Length > 0)
                {
                    var y = rel.First().Descendants("a").ToArray();
                    if (y.Any() && y.First().HasAttributes)
                    {
                        cache.RelUrl = WebUtility.UrlEncode(ParserPool.site_url + "/" + y.Last().Attributes["href"].Value);
                    }
                }
                #endregion

                #region Text
                var text = article.Descendants().First(x => x.HasAttributes && x.GetAttributeValue("id", "").Contains("item-desc"));
                var xr = text.Descendants().Where(x => x.InnerHtml.Contains("id=\"gallery") || x.HasClass("back") || x.HasAttributes && x.GetAttributeValue("role", "") == "photo").ToArray();
                if (xr.Any())
                {
                    for (var i = 0; i < xr.Length; i++)
                    {
                        if (text.ChildNodes.Contains(xr.ElementAt(i)))
                        {
                            text.RemoveChild(xr.ElementAt(i));
                        }
                    }
                }
                if (docs.Any())
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

                cache.Detailed.ContentHTML = @"" + (text.OuterHtml.Replace("%22", "%5C%22"));


            }
            catch (Exception ex)
            {
                MainApp.GetApp.Server.Logger.NotifyError(LogArea.Other, ex);
            }
            #endregion
        }

        private void TryUpdateImageURL(string imageUrl)
        {
            if (imageUrl != default)
            {
                ImageURL = imageUrl;
            }
        }

        internal async Task<NewsItem> FetchAsync()
        {
            await ProcessAsync().ConfigureAwait(false);
            return this;
        }
    }
}
