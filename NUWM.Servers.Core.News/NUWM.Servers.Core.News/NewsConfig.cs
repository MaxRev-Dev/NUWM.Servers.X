using MaxRev.Servers.Configuration;

namespace NUWM.Servers.Core.News
{
    public class NewsConfig : AbstractConfigContainer
    {
        public NewsConfig()
        {
            HostUrl = "http://nuwm.edu.ua";
            AbitUrl = "http://start.nuwm.edu.ua";
            TaskDelayMinutes = 10;
            TaskDelayHours = 1;
            OffsetLen = 5;
        }

        public string AbitUrl { get; set; }

        public string HostUrl { get; set; }

        public int TaskDelayMinutes { get; set; }
        public int TaskDelayHours { get; set; }
        public int PagesDefault { get; set; }
        public int CacheAlive { get; set; }
        public int OffsetLen { get; set; }
    }
}