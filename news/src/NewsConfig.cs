using System;
using System.Collections.Generic;
using MaxRev.Servers.Configuration;

namespace NUWM.Servers.Core.News
{
    public class NewsConfig : AbstractConfigContainer
    {
        public NewsConfig(IServiceProvider services)
        {

        }
        public int ParserOffsetMinutes { get; set; }
        public int ReparseTaskDelayMinutes { get; set; }
        public int ReparseTaskDelayHours { get; set; }
        public int CacheAliveHours { get; set; }
        public int DefaultPagesCount { get; set; }
        public List<NewsUrlDefinition> Urls { get; set; } = new List<NewsUrlDefinition>();
    }

    public class NewsUrlDefinition
    {
        public string Url { get; set; }
        public int InstituteID { get; set; } = -100;
    }
}