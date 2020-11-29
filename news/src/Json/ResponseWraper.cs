using Newtonsoft.Json;

namespace NUWEE.Servers.Core.News.Json
{
    public class ResponseWraper : Response
    {
        [JsonProperty("response")]
        public object ResponseContent { get; set; }
    }
}