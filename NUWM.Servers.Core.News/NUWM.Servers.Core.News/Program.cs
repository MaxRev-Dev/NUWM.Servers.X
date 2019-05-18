using System;
using System.Globalization;
using System.Threading.Tasks;
using MaxRev.Servers;
using MaxRev.Servers.Configuration;
using MaxRev.Servers.Interfaces; 

namespace NUWM.Servers.Core.News
{
    internal sealed class Program
    {
        private static void Main(string[] args)
        {
            var f = args?[0];
            if (string.IsNullOrEmpty(f))
            {
                Console.WriteLine("You must specify port");
                Environment.Exit(-1);
            }
            App.Get.Initialize(int.Parse(f));
        }
    }

    public class NewsConfig : AbstractConfigContainer
    {
        public NewsConfig()
        {
            HostUrl = "http://nuwm.edu.ua";
            AbitUrl = "http://start.nuwm.edu.ua";
        }

        public string AbitUrl { get; set; }

        public string HostUrl { get; set; }

        public int TaskDelayMinutes { get; set; }
        public int TaskDelayHours { get; set; }
        public int PagesDefault { get; set; }
        public int CacheAlive { get; set; }
    }
    internal sealed class App
    {
        public enum Dirs
        {
            Addons,
            AddonsNews,
            Log,
            Cache
        }

        private static App app;
        private ConfigManager<NewsConfig> _configManager;
        public ParserPool ParserPool { get; private set; }
        public static App Get => app ?? (app = new App());
        public static int OffsetLen { get; internal set; }
        public IReactor Core { get; private set; }
        public NewsConfig Config => _configManager.ConfigInstance;

        public void Initialize(int port)
        {
            ParserPool = new ParserPool();
            var cultureInfo = new CultureInfo("uk-UA");
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            _configManager = new ConfigManager<NewsConfig>(new ConfiguratorSettings(Core, configFileName: "news_config.json"));

            ReactorStartup.From(default, new ReactorStartupConfig { AutoregisterControllers = true })
                .Configure((with, core) =>
                {
                    Core = core;
                    with.Server("News", port, OnServerStart);

                }).Run();
            //  Core.Config.Core.ServerHeaderName = "NUWM.Servers.News by MaxRev";

            // Server = Core.GetServer("News", port) as IServer;

        }



        private void OnServerStart(IServer Server)
        {
            Server.EventMaster.Suspending += async (s, e) =>
            {
                await ParserPool.SaveCache();
            };
            Server.DirectoryManager.AddDir(Dirs.Addons, "addons");
            Server.DirectoryManager.AddDir(Dirs.Addons, Dirs.AddonsNews, "news");
            Server.DirectoryManager.AddDir(Dirs.Cache, "cache");
            Server.DirectoryManager.AddDir(Dirs.Log, "log");
            // Server.Config.Main.ServerTypeName = new KeyValuePair<string, string>("X-NS-Type", "News");
            Server.ConfigManager.Save();
#if DEBUG
            Console.WriteLine("Server starting");
#endif
            Task.Run(() =>
            {
                ParserPool.LoadInstantCache();
                ParserPool.SetCurrent();
                ParserPool.BaseInitParsers();
            });

        }
    }
}
