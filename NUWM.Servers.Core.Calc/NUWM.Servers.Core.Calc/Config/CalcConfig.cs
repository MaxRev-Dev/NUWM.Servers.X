using System;
using System.Collections.Generic;
using System.Linq;
using MaxRev.Servers.Configuration;

namespace NUWM.Servers.Core.Calc.Config
{
    public class CalcConfig : AbstractConfigContainer
    {
        public TimeSpan UpdateDelay { get; set; }
        public double UkrOlimpMark { get; }
        public Dictionary<string, InfoNode> FetchMap { get; set; }

        public CalcConfig(IServiceProvider _)
        {
            UpdateDelay = TimeSpan.FromHours(1);
            UkrOlimpMark = 20; 
            FetchMap = new Dictionary<string, InfoNode>
            {
                {"ua_olimp_info", ((InfoNode)"/html/body/div[1]/div/div/div/section/div[2]/article/div|http://start.nuwm.edu.ua/olimpiada")},
                {"nuwm_prep_info",((InfoNode)"/html/body/div[2]/div/div/div/section/article|http://nuwm.edu.ua/navchaljno-naukovi-instituti/zaochno-distancijnogho-navchannja/viddilennja-dovuzivsjkoji-pidghotovkita-profiljnogho-navchannja/pidghotovchi")},
            };
        }
    }

    public class InfoNode
    {
        public string Url { get; private set; }
        public string XPath { get; private set; }

        public static explicit operator InfoNode(string val)
        {
            var tx = val.Split(',', '|').ToList();
            var t = tx.FirstOrDefault(x => x.StartsWith("http"));
            var index = tx.IndexOf(t);
            return new InfoNode { Url = tx[index], XPath = tx[index != 1 ? 1 : 0] };
        }
    }
}