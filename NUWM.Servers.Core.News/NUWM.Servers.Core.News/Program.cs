using System;
using System.Globalization;
using System.Threading.Tasks;
using MaxRev.Servers;
using MaxRev.Servers.Configuration;
using MaxRev.Servers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

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

    internal sealed class App
    {
        public enum Dirs
        {
            Addons,
            AddonsNews,
            Log,
            Cache
        }

        private static App _app;

        private ConfigManager<NewsConfig> _configManager;
        public ParserPool ParserPool { get; private set; }
        public static App Get => _app ?? (_app = new App()); 
        public IReactor Core { get; private set; }
        public NewsConfig Config => _configManager.ConfigInstance;

        public void Initialize(int port)
        {
            var cultureInfo = new CultureInfo("uk-UA");
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            ReactorStartup.From(default, new ReactorStartupConfig { AutoregisterControllers = true })
                .Configure((with, core) =>
                {
                    Core = core;
                    _configManager = new ConfigManager<NewsConfig>(new ConfiguratorSettings(Core, configFileName: "news_config.json"));

                    with.Server("News", port, OnServerStart);
                    with.Services(coll =>
                    {
                        coll.AddTransient<ParserPool>();
                        coll.AddSingleton(Config);
                    });
                }).Run();
        }



        private void OnServerStart(IServer Server)
        { 
            Core.DirectoryManager.AddDir(Dirs.Addons, "addons");
            Core.DirectoryManager.AddDir(Dirs.Addons, Dirs.AddonsNews, "news");
            Core.DirectoryManager.AddDir(Dirs.Cache, "cache");
            Core.DirectoryManager.AddDir(Dirs.Log, "log");

            ParserPool = new ParserPool(Config);
            Server.EventMaster.Suspending += async (s, e) =>
            {
                await ParserPool.SaveCache();
            };
#if DEBUG
            Console.WriteLine("Server starting");
#endif
            Task.Run(() =>
            {
                ParserPool.LoadInstantCache();
                ParserPool.SetCurrent();
                ParserPool.BaseInitParsers(Config);
            });
        }
    }
}
