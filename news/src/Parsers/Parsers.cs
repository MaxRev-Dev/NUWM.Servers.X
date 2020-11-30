using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils;
using Newtonsoft.Json;
using NUWEE.Servers.Core.News.Config;
using NUWEE.Servers.Core.News.Json;
using NUWEE.Servers.Core.News.Parsers;

namespace NUWEE.Servers.Core.News.Parsers
{
    public class NewsItemVisualizer
    {
        [JsonProperty("item")]
        public List<NewsItem> NewsItemList { get; set; }
    }

}

namespace NUWEE.Servers.Core.News.Json
{
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
            var doc = new HtmlDocument();
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

                var doc = new HtmlDocument();
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
                            var uri = i.FirstChild.Attributes["src"].Value;

                            var finalUri = ParserPool.site_url + uri.Substring(1); 
                            cache.Detailed.ImagesLinks.Add(finalUri);
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
