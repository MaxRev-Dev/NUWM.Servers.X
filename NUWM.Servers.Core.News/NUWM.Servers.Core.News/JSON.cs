using MR.Servers.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace JSON
{

    public class Response
    {
        [JsonProperty("code")]
        public StatusCode Code { get; set; }
        [JsonProperty("cache")]
        public bool Cache { get; set; }
        [JsonProperty("error")]
        public object Error { get; set; }
        [JsonProperty("response")]
        public object Content { get; set; }
    }
    public class ResponseWraper : Response
    {
        [JsonProperty("response")]
        public object ResponseContent { get; set; }
    }
    [Serializable]
    public partial class NewsItem
    {
        [Serializable]
        public class DocItem
        {
            public DocItem(string name, string url, string type)
            {
                Name = name;
                Url = url;
                Type = type;
            }
            [JsonProperty("url")]
            public string Url { get; set; }
            [JsonProperty("type")]
            public string Type { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
        }
        public partial class NewsItemDetailed
        {
            [JsonProperty("content")]
            public string ContentHTML { get; set; }
            [JsonProperty("g_images")]
            public List<string> ImagesLinks { get; set; }
            [JsonProperty("docs")]
            public List<DocItem> DocsLinks { get; set; }
        }
        [JsonProperty("cache_age")]
        public string CachedOnStr { get { return CachedOn.Ticks.ToString(); } }
        [JsonIgnore]
        public DateTime CachedOn;
        public NewsItem()
        {
            CachedOn = TimeChron.GetRealTime();
        }
        string img;
        [JsonProperty("image_url")]
        public string ImageURL
        {
            get { return img; }
            set { img = value?.Replace("170x140", "220x180"); }
        }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("url")]
        public string Url { get; set; }
        [JsonProperty("date")]
        public string Date { get; set; }
        [JsonProperty("excerpt")]
        public string Excerpt { get; set; }
        [JsonProperty("detailed")]
        public NewsItemDetailed Detailed { get; set; }
        [JsonProperty("related")]
        public string RelUrl { get; set; }
        //[JsonProperty("page_id")]
        // public int PageId { get; set; }
    }
    public enum StatusCode
    {
        Undefined = 1,
        InvalidRequest = 32,
        NotFound = 33,
        AccessDenied = 60,
        DeprecatedMethod = 66,
        ServerSideError = 88,
        GatewayTimeout,
        Success = 100
    }
}
