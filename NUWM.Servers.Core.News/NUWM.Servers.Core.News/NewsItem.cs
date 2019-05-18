using HtmlAgilityPack;

namespace NUWM.Servers.Core.News
{
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
    }
}