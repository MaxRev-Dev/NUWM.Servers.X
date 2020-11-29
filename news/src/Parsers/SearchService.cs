using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MaxRev.Utils;
using NUWEE.Servers.Core.News.Json;

namespace NUWEE.Servers.Core.News.Parsers
{
    internal class SearchService
    {
        private readonly ParserPool _parserPool;

        public SearchService(ParserPool parserPool)
        {
            _parserPool = parserPool;
        }

        /// <exception cref="T:System.IO.InvalidDataException">Not found</exception>
        public async Task<(List<NewsItem>, bool)> QueryAsync(string query, int count)
        {
            var searchDoc = new HtmlAgilityPack.HtmlDocument();
            using (var r = new Request("http://nuwm.edu.ua/search?text=" + query.Replace(' ', '+')))
            {
                using (var rm = await RequestAllocator.Instance.UsingPoolAsync(r).ConfigureAwait(false))
                using (var s = await rm.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    searchDoc.Load(s);
            }

            var wnode = searchDoc.DocumentNode.Descendants()
                .Where(x => x.Name == "div"
                            && x.HasClass("news")
                            && x.HasClass("search")
                            && x.GetAttributeValue("role", "") == "group").ToArray();
            if (!wnode.Any())
            {
                throw new InvalidDataException("Not found");
            }

            var node = wnode.First();
            var instantCache = _parserPool.InstantCache.InstantCacheList;

            var news = new List<NewsItem>();

            var virg = true;

            foreach (var a in node.Elements("article"))
            {
                var btnf = a.Descendants("a")
                    .Where(x => x.HasClass("btn")
                                && x.HasClass("s2")).ToArray();
                if (btnf.Any())
                {
                    var link = btnf.First().GetAttributeValue("href", "");
                    if (link.Contains("/news"))
                    {
                        var found = false;
                        foreach (var i in _parserPool.Values)
                        {
                            var t = i.Newslist.Where(x => x.Url == link).ToArray();
                            if (t.Length == 1)
                            {
                                found = true;
                                news.Add(t.First());
                                break;
                            }

                        }
                        if (instantCache != null)
                        {
                            var inst = instantCache.Where(x => x.Url == link).ToArray();
                            if (inst.Length == 1)
                            {
                                news.Add(inst.First());
                                found = true;
                            }
                        }
                        if (!found)
                        {
                            var u = new NewsItem
                            {
                                Excerpt = a.Descendants("p").First().InnerText,
                                Title = a.Descendants("a").First(x => x.HasClass("name")).InnerText,
                                Url = link
                            };

                            await u.FetchAsync().ConfigureAwait(false);
                            news.Add(u);
                            if (instantCache == null)
                            {
                                instantCache = new List<NewsItem>();
                            }
                            virg = false;
                            instantCache.Add(u);
                        }
                    }
                }
                if (news.Count == count)
                {
                    break;
                }
            }

            return (news, virg);
        }
    }
}