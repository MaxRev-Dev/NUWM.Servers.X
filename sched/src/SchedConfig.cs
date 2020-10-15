using MaxRev.Servers.Configuration;

namespace NUWM.Servers.Core.Sched
{
    public class SchedConfig : AbstractConfigContainer
    {
        public SchedConfig()
        {
            BaseUrl = "https://desk.nuwm.edu.ua/cgi-bin/timetable.cgi?n=700";
        }

        public string BaseUrl { get; set; }
    }
}