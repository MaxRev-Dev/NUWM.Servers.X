using System;

namespace NUWM.Servers.Core.Uptime
{
    sealed class Program
    {
        static void Main(string[] args)
        { 
            var f = args?[0]?.ToString();
            if (string.IsNullOrEmpty(f))
            {
                Console.WriteLine("You must specify port");
                Environment.Exit(-1);
            }
            new MainApp().Initialize(int.Parse(f));
        }
    }
    [Serializable]
    sealed class MainApp
    {
        public static string[] dirs = new[] {
                "./addons", 
                "./log"
        };
        public static MainApp Current;
        public void Initialize(int port)
        {
            Current = this;
            Core.Reactor.CheckDirs(dirs);
            new Core.Reactor(port, "NUWM.Uptime");
            Core.Reactor.Current.SetAPIHandler(new Core.Reactor.Client.ApiHandler(HandlerForApi));
            Core.Reactor.Current.SetStartFinilizeHandler(new Core.Reactor.Server.StartFinalizingHandler(OnServerStart));
            Core.Reactor.Current.SetStatsInvokers(new Core.Reactor.StatsInvoker(GetServerVersion), new Core.Reactor.StatsInvoker(GetReqsCount));
            Core.Reactor.Current.RunServerOnPort();
        }
        string GetReqsCount() => APIUtilty.API.ApiRequestCount.ToString();
        string GetServerVersion() => GetVersion().ToString();
        void OnServerStart()
        {
            new Uptime.UptimePool().Initialize();
        }
        public static Version GetVersion() => System.Reflection.Assembly.GetAssembly(typeof(MainApp)).GetName().Version;

        Tuple<int, string, string> HandlerForApi(Core.Reactor.Client.ClientInfo obj)
        {
            string content = "", type = "";
            try
            {
#if DEBUG
                Console.WriteLine("\n\nClient API handler start");
#endif
                APIUtilty.API api = new APIUtilty.API(obj.Query, obj.Headers);
                var task = api.PrepareForResponse(obj.Request, obj.Content, obj.Action);
                content = task.Item1;
                type = task.Item2;
            }
            catch (Exception ex)
            {
                content = APIUtilty.API.CreateErrorResp(ex);
                type = "text/json";
            }
            return new Tuple<int, string, string>(200, content, type);
        }
    }
}
