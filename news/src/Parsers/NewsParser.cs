using System;
using System.Collections.Generic;
using System.Globalization;
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
    internal class NewsParser : AbstractParser
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
                        if (_newsList == null)
                        {
                            _newsList = new List<NewsItem>();
                        }

                        var op = ParseInstance(new Tuple<HtmlNode, int, string>(news_art, 1, Url)).ToArray();

                        _newsList.AddRange(_newsList.Count > 0 ? op.Where(x => _newsList.All(y => x.Url != y.Url)) : op);

                        var tasks = op.Where(x => x.Detailed == null || x.Detailed == new NewsItem.NewsItemDetailed())
                            .Select(x => x.FetchAsync()).Cast<Task>().ToArray();
                        Task.WaitAll(tasks);

                        int pages_count = Convert.ToInt16(news_art.NextSibling.ChildNodes[news_art.NextSibling.ChildNodes.Count - 4].InnerText);
                        var pagesDef = pages_count < MainApp.Config.DefaultPagesCount ? pages_count : MainApp.Config.DefaultPagesCount;

                        for (var id = 2; id < pagesDef + 1; id++)
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

                                    _newsList.AddRange(items.Where(x => _newsList.All(y => x.Url != y.Url)));

                                    Task.WaitAll(items.Select(x => x.FetchAsync()).Cast<Task>().ToArray());

                                }
                            }
                        }

                        try
                        {
                            var cu = CultureInfo.CreateSpecificCulture("uk-UA");
                            var t = _newsList.First().Date;
                            if (DateTime.TryParseExact(t, "dd MMMM yyyy", cu, DateTimeStyles.None, out _))
                            {
                                _newsList = _newsList.OrderByDescending(x =>
                                    DateTime.ParseExact(x.Date, "dd MMMM yyyy", cu)).ToList();
                            }
                            else
                            {
                                _newsList = _newsList.OrderByDescending(x => DateTime.Parse(x.Date, cu)).ToList();
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