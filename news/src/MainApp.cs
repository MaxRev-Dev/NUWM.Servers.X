using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using APIUtilty;
using Lead;
using MaxRev.Servers;
using MaxRev.Servers.Configuration;
using MaxRev.Servers.Core.Events;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils.FileSystem;
using Microsoft.Extensions.DependencyInjection;

namespace NUWM.Servers.Core.News
{
    internal sealed class MainApp
    {
        internal static NewsConfig Config => GetApp.NewsConfig.ConfigInstance;

        public enum Dirs { Addons, AddonsNews, Log, Cache }

        private static MainApp app;
        public static MainApp GetApp => app ?? (app = new MainApp());
        public IServer Server { get; private set; }
        public IReactor Core { get; private set; }
        public ConfigManager<NewsConfig> NewsConfig { get; private set; }
        public DirectoryManager<Dirs> DirectoryManager { get; set; }

        public Task InitializeAsync(int port, string[] args)
        {
            var cultureInfo = new CultureInfo("uk-UA");
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            var runtime = ReactorStartup.From(args, new ReactorStartupConfig
            {
                AutoregisterControllers = true
            });
            runtime.ConfigureLogger(new LoggerConfiguration{LoggingAreas = LogArea.Full, WriteToConsole = true}).Configure((with, core) =>
            {
                Core = core;
                Core.Config.Basic.ServerHeaderName = "NUWM.Servers.News by MaxRev";

                Server = Core.GetServer("News", port);
                
                with.Services(c =>
                {
                    c.AddSingleton(x => core.Logger);
                    c.AddSingleton<ParserPool>();
                    c.AddSingleton<SearchService>();
                    c.AddSingleton<ParserFactory>();
                    c.AddTransient<NewsParser>();
                    c.AddTransient<AbitNewsParser>();
                    c.AddSingleton<CacheManager>();
                    c.AddTransient<PoolParserScheduler>();
                    c.AddSingleton<InstantCacheSaveScheduler>();
                    c.AddSingleton<InstantCacher>();
                }); 
                Server.Features.GetFeature<IServerEvents>().ServerStarting += OnServerStart;

                Core.ConfigManager.Save();
                Core.UnhandledException += (s, e) =>
                {
                    TrySaveAsync(Core.Services.GetRequiredService<CacheManager>()).Wait();
                };
                Server.Features.GetFeature<IServerEvents>().Suspending += (s, e) =>
                {
                    var _cacheManager = Server.Parent.Services.GetRequiredService<CacheManager>();
                    TrySaveAsync(_cacheManager).Wait();
                };

            });
            return runtime.RunAsync();
        }

        private static Task TrySaveAsync(CacheManager cacheManager)
        {
            return cacheManager.SaveCacheAsync();
        }


        private void OnServerStart(IServer s, object e)
        {
            NewsConfig =
                new ConfigManager<NewsConfig>(new ConfiguratorSettings(Server, default, "news_config.json"));
            var dm = DirectoryManager
                = new DirectoryManager<Dirs>(Directory.GetCurrentDirectory());
            dm.AddDir(Dirs.Addons, "addons");
            dm.AddDir(Dirs.Addons, Dirs.AddonsNews, "news");
            dm.AddDir(Dirs.Cache, "cache");

            //Server.Config.Main.ServerTypeName = new KeyValuePair<string, string>("X-NS-Type", "News"); 

            Task.Run(() =>
            {
                Server.Parent.Services.GetRequiredService<ParserPool>().InstantCache.Load();
            });
        }

    }
}