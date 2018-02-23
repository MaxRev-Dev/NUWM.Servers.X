using Newtonsoft.Json;

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

    public class ScheduleVisualiser
    {
        [JsonProperty("schedule")]
        public object Data { get; set; }
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
