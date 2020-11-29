using Newtonsoft.Json;
using NUWM.Servers.Core.News.Json;

namespace NUWEE.Servers.Core.News.Json
{
    public class ResponseV2 : Response
    {
        public ResponseV2(Response response)
        {
            Code = response.Code;
            Cache = response.Cache;
            Error = response.Error;
            Content = response.Content;
        }

        [JsonProperty("successful")]
        public bool IsSuccessful => Code == StatusCode.Success;
    }
}