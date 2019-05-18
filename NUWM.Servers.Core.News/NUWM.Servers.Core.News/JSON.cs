using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MaxRev.Utils;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.News
{
    public class NewsItemVisualizer
    {
        [JsonProperty("item")]
        public List<NewsItem> NewsItemList { get; set; }
    }


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

    [Serializable]
    public partial class NewsItem
    {
        [JsonProperty("cache_age")]
        public string CachedOnStr => CachedOn.Ticks.ToString();
        [JsonIgnore]
        public DateTime CachedOn;
        public NewsItem()
        {
            CachedOn = TimeChron.GetRealTime();
        }

        private string img;
        [JsonProperty("image_url")]
        public string ImageURL
        {
            get => img;
            set => img = value != null ? Regex.Replace(value, @"\d*x\d*", "0") : null;
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
