using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MaxRev.Utils;
using Newtonsoft.Json;

namespace NUWEE.Servers.Core.News.Json
{
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
        [Serializable]
        public class NewsItemDetailed
        {
            [JsonProperty("content")]
            public string ContentHTML { get; set; }
            [JsonProperty("g_images")]
            public List<string> ImagesLinks { get; set; }
            [JsonProperty("docs")]
            public List<DocItem> DocsLinks { get; set; }
        }
        [JsonProperty("cache_age")]
        public string CachedOnStr => CachedOn.Ticks.ToString();
        [JsonIgnore]
        public DateTime CachedOn;
        public NewsItem()
        {
            CachedOn = TimeChron.GetRealTime();

        }
        [JsonProperty("image_cached")]
        public bool ImageCached { get; private set; }
        private string img;
        [JsonProperty("image_url")]
        public string ImageURL
        {
            get => img;
            set {
                if (value != null)
                {
                    if (ImageCached) return;
                    img = value;

                    if (Utils.OriginalImageCheck(ref img))
                        ImageCached = true;
                }
                else
                    img = null;
            }
        }

        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("url")]
        public string Url { get; set; }
        [JsonProperty("date")]
        public string Date { get; set; }

        private static readonly CultureInfo _cu = new CultureInfo("uk-UA");


        [JsonProperty("date_h")]
        public string HuDate
        {
            get {
                DateTime d;
                var now = TimeChron.GetRealTime();
                try
                {
                    d = DateTime.Parse(Date, _cu);
                }
                catch (FormatException)
                {
                    return Date;
                }

                string TryReturnFromContext(int e, bool cust = false, params string[] vs)
                {
                    switch (e)
                    {
                        case 1:
                            return cust ? vs.First() : "Минулого " + vs.First();
                        case 2:
                            return cust ? vs[vs.Length - 2] : "Позаминулого " + vs[vs.Length - 2];
                        default:
                            if (e > 2)
                                return cust ? vs.Last() : $"Кілька {vs.Last()} тому";
                            break;
                    }

                    return default;
                }

                var r = TryReturnFromContext(now.Year - d.Year, false, "року", "років");
                if (r != default) return r;
                r = TryReturnFromContext(now.Month - d.Month, false, "місяця", "місяців");
                if (r != default) return r;
                r = TryReturnFromContext(Utils.GetIso8601WeekOfYear(now) -
                                         Utils.GetIso8601WeekOfYear(d), false,
                    "тижня", "тижнів");
                if (r != default) return r;
                r = TryReturnFromContext(now.Day - d.Day, true,
                    "Вчора", "Позавчора", "Кілька днів тому");
                if (r != default) return r;
                r = TryReturnFromContext(now.Hour - d.Hour, true,
                    "Годину тому", "Дві години тому", "Кілька годин тому");
                if (r != default) return r;

                return Date;
            }
        }

        [JsonProperty("excerpt")]
        public string Excerpt { get; set; }
        [JsonProperty("detailed")]
        public NewsItemDetailed Detailed { get; set; }
        [JsonProperty("related")]
        public string RelUrl { get; set; }

    }
}