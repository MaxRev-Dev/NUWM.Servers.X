using Newtonsoft.Json;
using NUWM.Servers.Core.News.Json;

namespace NUWEE.Servers.Core.News.Json
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
}