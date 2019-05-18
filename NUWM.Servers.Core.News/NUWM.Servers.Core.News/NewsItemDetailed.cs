using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.News
{
    [Serializable]
    public class NewsItemDetailed
    {
        public static async Task Process(NewsItem item)
        {
            var mess = await RequestAllocator.Instance.UsingPool(new Request(item.Url)); 
            HtmlDocument doc = new HtmlDocument();
            if (mess.IsSuccessStatusCode)
            {
                doc.LoadHtml(mess.Content.ReadAsStringAsync().Result);
                ParseArticle(item, doc.DocumentNode.Descendants().First(x => x.Name == "article" && x.HasClass("item-detailed")));
            }
        } 
        public static void ParseArticle(NewsItem item, HtmlNode artc)
        {
            try
            {
                item.Detailed = new NewsItemDetailed();
                if (string.IsNullOrEmpty(item.Date))
                {
                    item.Date = artc.Descendants("time").First().InnerText;
                    var img = artc.Descendants("img").First().GetAttributeValue("src", "");
                    item.ImageURL = App.Get.Config.HostUrl + img.Substring(1);
                }
                #region Docs
                var docs = artc.Descendants().Where(x => x.HasAttributes && x.GetAttributeValue("class", "null").Contains("file-desc"));
                var htmlNodes = docs as HtmlNode[] ?? docs.ToArray();
                if (htmlNodes.Any())
                {
                    item.Detailed.DocsLinks = new List<DocItem>();
                    foreach (var doc in htmlNodes)
                    {
                        var f = doc.NextSibling;
                        var t = f.ChildNodes[0].GetAttributeValue("data-href", "");
                        var type = f.ChildNodes[0].GetClasses().First(x => x != "img" && x != "ib");
                        item.Detailed.DocsLinks.Add(new DocItem(doc.InnerText, @"" + WebUtility.UrlEncode(t), type));
                    }
                }
                #endregion
                #region Images
                var imgnode = artc.Descendants().Where(x =>
                    x.Name == "div" && x.HasClass("s1") && x.GetAttributeValue("role", "") == "marquee").ToArray();


                if (imgnode.Length > 0)
                {
                    item.Detailed.ImagesLinks = new List<string>();
                    foreach (var y in imgnode)
                    {
                        var box = y.ChildNodes.Single(x => x.HasClass("box"));
                        foreach (var i in box.FirstChild.ChildNodes)
                        {
                            string uri = i.FirstChild.Attributes["src"].Value;
                            uri = uri.Replace(new Regex(@"(?<=photo.).*(?=\/)").Match(uri).Value, "0");
                            item.Detailed.ImagesLinks.Add(App.Get.Config.HostUrl + uri.Substring(1));
                        }
                    }
                }
                #endregion
                #region RelatedLink
                var rel = artc.Descendants().Where(x => x.InnerText.Contains("Читайте також")).ToArray();
                if (rel.Length > 0)
                {
                    var y = rel.First().Descendants("a").ToArray();
                    if (y.Any() && y.First().HasAttributes)
                    {
                        item.RelUrl = WebUtility.UrlEncode(App.Get.Config.HostUrl + "/" + y.Last().Attributes["href"].Value);
                    }
                }
                #endregion

                #region Text
                var text = artc.Descendants().First(x => x.HasAttributes && x.GetAttributeValue("id", "").Contains("item-desc"));
                var xr = text.Descendants().Where(x => x.InnerHtml.Contains("id=\"gallery") ||
                                                       x.HasClass("back") ||
                                                       x.HasAttributes && x.GetAttributeValue("role", "") == "photo").ToArray();
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
                if (htmlNodes.Any())
                {
                    var v = htmlNodes.First().ParentNode;
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
                App.Get.Core.Logger.NotifyError(LogArea.Other, ex);
            }
            #endregion
        }

        [JsonProperty("content")]
        public string ContentHTML { get; set; }
        [JsonProperty("g_images")]
        public List<string> ImagesLinks { get; set; }
        [JsonProperty("docs")]
        public List<DocItem> DocsLinks { get; set; }
    }
}