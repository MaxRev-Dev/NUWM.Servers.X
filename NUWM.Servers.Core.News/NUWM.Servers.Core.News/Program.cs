using Lead;
using MR.Servers;
using MR.Servers.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace NUWM.Servers.Core.News
{
    internal sealed class Program
    {
        private static void Main(string[] args)
        {
            var f = args?[0]?.ToString();
            if (string.IsNullOrEmpty(f))
            {
                Console.WriteLine("You must specify port");
                Environment.Exit(-1);
            }
            MainApp.GetApp.Initialize(int.Parse(f));
        }
    }

    internal sealed class MainApp
    {
        public static int AllUsersCount = 0;
        internal static int taskDelayH;
        internal static int cacheAlive;

        public static int taskDelayM, pagesDef; 
        public enum Dirs { Addons, AddonsNews, Log, Cache }
        private static int port;
        private static MainApp app;
        public static MainApp GetApp => app ?? (app = new MainApp());
        public Server Server { get; private set; }
        public Reactor Core { get; } = new Reactor();
        public void Initialize(int port)
        {
            var cultureInfo = new CultureInfo("uk-UA");
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            MainApp.port = port; 
            Core.UnhandledException += OnUnhandledException;
            Core.Config.Core.ServerHeaderName = "NUWM.Servers.News by MaxRev";
             


            Server = Core.GetServer("News", port) as Server;

            Server.EventMaster.ServerStarting += OnServerStart;

            Core.ConfigManager.Save();
            Server.EventMaster.Suspending += async (s, e) =>
            {
                await (ParserPool.Parser.SaveCache());
            };
            Core.Listen(Server).Wait();
        }
         
         

        private void OnServerStart(IServer s, object e)
        {
            Server.DirectoryManager.AddDir(Dirs.Addons, "addons");
            Server.DirectoryManager.AddDir(Dirs.Addons, Dirs.AddonsNews, "news");
            Server.DirectoryManager.AddDir(Dirs.Cache, "cache");
            Server.DirectoryManager.AddDir(Dirs.Log, "log");
            Server.Config.Main.ServerTypeName = new KeyValuePair<string, string>("X-NS-Type", "News");
            Server.ConfigManager.Save();
            Server.SetApiController(typeof(APIUtilty.API));
#if DEBUG
            Console.WriteLine("Server starting");
#endif
            Lead.ParserPool.Parser.LoadInstantCache();

            #region ParserPoolThread
            var CurrentParserPool = new Lead.ParserPool();
            CurrentParserPool.SetCurrent();
            CurrentParserPool.BaseInitParsers();
            #endregion
            new Lead.ParserPool.CacheUpdater();
            Lead.ParserPool.Parser.ScheduleInstantCacheSave.Schedule_Timer();
        }
        public static Version GetVersion()
        {
            return System.Reflection.Assembly.GetAssembly(typeof(MainApp)).GetName().Version;
        }

        public static void Restart()
        {
            new Task(new Action(() =>
            {
                MainApp.GetApp.Server.StopListen();
                Task.Delay(1000 * 65).Wait();
                Process.Start(new ProcessStartInfo("dotnet", string.Format("NUWM.Servers.Core.News.dll {0}", port)));
                Environment.Exit(0);
            })).Start();

        }

        private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            await Lead.ParserPool.Parser.SaveCache();
        }
         
    }
}
