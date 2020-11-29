using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils;
using NUWEE.Servers.Core.News.Json;

namespace NUWEE.Servers.Core.News.Parsers
{
    [Serializable]
    internal class AbitNewsParser : AbstractParser
    {
        public AbitNewsParser(ILogger logger) : base(logger)
        {
        }

        private async Task<List<NewsItem>> AbitNewsParserAsync(string parser_url)
        {
            var items = new List<NewsItem>();
            try
            {
                using (var request = new Request(parser_url))
                {
                    var r = await RequestAllocator.Instance.UsingPoolAsync(request).ConfigureAwait(false);
                    if (r.IsSuccessStatusCode)
                    {
                        var doc = new HtmlDocument();
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
                                        var docx = new HtmlDocument();
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
                            var docx1 = new HtmlDocument();
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
                string DecodeReplace(string val) =>
                    WebUtility.HtmlDecode(val)?
                        .Replace('\n', ' ')
                        .Replace('\t', ' ')
                        .Trim();

                var text = txt.OuterHtml;
                title = DecodeReplace(title);
                date = DecodeReplace(date);

                var item = new NewsItem(lurl, title, date, img)
                {
                    Detailed = new  NewsItem.NewsItemDetailed(),
                    Excerpt = default
                };
                item.Detailed.ContentHTML = text.Replace("%22", "%5C%22");
                items.Add(item);
            }
            catch (Exception ex) { _logger.NotifyError(LogArea.Other, ex); }


            return items;
        }

        public override async Task ParsePagesAsync(string parser_url)
        {
            _newsList.AddRange(await AbitNewsParserAsync(parser_url).ConfigureAwait(false));
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
}