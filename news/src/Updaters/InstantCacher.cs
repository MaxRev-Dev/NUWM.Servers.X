using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUWEE.Servers.Core.News.Json;
using NUWEE.Servers.Core.News.Parsers;

namespace NUWEE.Servers.Core.News.Updaters
{
    public class InstantCacher
    {
        #region InstantCacheImpl
        public enum InstantState
        {
            Success,
            TimedOut,
            ErrorParsing,
            ConnectionWithServerError,
            FromCache
        }
        public List<NewsItem> InstantCacheList = new List<NewsItem>();
        internal readonly string InstantCachePath = Path.Combine(
            MainApp.GetApp.DirectoryManager[MainApp.Dirs.Cache], "instantCache.txt");

        private readonly IServiceProvider _services;


        public InstantCacher(IServiceProvider services)
        {
            _services = services;
        }

        public async Task SaveInstantCacheAsync()
        {
            try
            {
                if (InstantCacheList != null && InstantCacheList.Count > 0)
                {
                    await File.WriteAllTextAsync(InstantCachePath, JsonConvert.SerializeObject(InstantCacheList)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                MainApp.GetApp.Server.Logger.NotifyError(LogArea.Other, ex);
            }
        }
        public async void Load()
        {
            if (File.Exists(InstantCachePath))
            {
                try
                {
                    using (var fl = File.OpenText(InstantCachePath))
                    {
                        var t = await fl.ReadToEndAsync().ConfigureAwait(false);
                        InstantCacheList = JsonConvert.DeserializeObject<List<NewsItem>>(t); 
                    }
                }
                catch (Exception ex)
                {
                    MainApp.GetApp.Server.Logger.NotifyError(LogArea.Other, ex);
                }
            }
        }
        public async Task<Tuple<NewsItem, InstantState>> ParsePageInstantAsync(string url, bool html)
        {
            try
            {
                if (InstantCacheList == null)
                {
                    InstantCacheList = new List<NewsItem>();
                }

                var state = InstantState.FromCache;
                if (InstantCacheList != null && InstantCacheList.Count > 0)
                {
                    var spl = url.Substring(url.LastIndexOf('/'));
                    if (url.EndsWith('/'))
                    {
                        _ = url.Substring(0, url.Length - 2);
                    }

                    var _parserPool = _services.GetRequiredService<ParserPool>();
                    var item = _parserPool.Values
                        .SelectMany(x => x.Newslist)
                        .Union(InstantCacheList)
                        .FirstOrDefault(x => x.Url.Contains(spl));
                    if (item != default)
                    {
                        var obj = Utils.DeepCopy(item);
                        obj.Detailed.ContentHTML = html ? item.Detailed.ContentHTML : item.GetText();
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
                using (var request = new Request(url))
                {
                    var rm = await RequestAllocator.Instance.UsingPoolAsync(request)
                        .ConfigureAwait(false);
                    if (rm != null && rm.IsSuccessStatusCode && rm.Content != null)
                    {
                        var doc = new HtmlDocument();
                        var tg = await rm.Content.ReadAsStringAsync().ConfigureAwait(false);
                        doc.LoadHtml(tg);
                        var dla = doc.DocumentNode.Descendants("article").First();
                        var title = dla.Element("h1").InnerText;
                        var date = dla.Element("time").InnerText;
                        var imgAttributeValue = dla.Element("img").GetAttributeValue("src", "");
                        var imgUrl = ParserPool.site_url +
                                     imgAttributeValue.Substring(1, imgAttributeValue.Length - 1);

                        var item = new NewsItem(url, title, date, imgUrl);

                        NewsItem.ParseArticle(item,
                            doc.DocumentNode.Descendants()
                                .First(x => x.Name == "article" && x.HasClass("item-detailed")));

                        InstantCacheList.Add(item);

                        var ret = Utils.DeepCopy(item);
                        ret.Detailed.ContentHTML = html ? ret.Detailed.ContentHTML : ret.GetText();
                        return new Tuple<NewsItem, InstantState>(ret, InstantState.Success);
                    }
                    else
                    {
                        return new Tuple<NewsItem, InstantState>(null, InstantState.TimedOut);
                    }
                }
            }
            catch (Exception)
            {
                return new Tuple<NewsItem, InstantState>(null, InstantState.ErrorParsing);
            }

        }
        #endregion
    }
}