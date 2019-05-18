using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using MaxRev.Servers;
using MaxRev.Servers.Configuration;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Utils.Schedulers;
using Newtonsoft.Json;
using MaxRev.Utils.Http;
using MaxRev.Servers.Core.Http;

namespace NUWM.Servers.Core.Calc
{
    internal sealed class Program
    {
        private static Task Main(string[] args)
        {
            var f = args?[0];
            if (string.IsNullOrEmpty(f))
            {
                Console.WriteLine("You must specify port");
                Environment.Exit(-1);
            }
            return App.Get.Initialize(args);
        }
    }

    public class CalcConfig : AbstractConfigContainer
    {
        public TimeSpan UpdateDelay { get; set; }

        public CalcConfig()
        {
            UpdateDelay = TimeSpan.FromHours(1);
        }
    }
    internal sealed class App
    {
        private static App _app;
        public static App Get => _app ?? (_app = new App());

        public CalcConfig Config => _configManager.ConfigInstance;
        private ConfigManager<CalcConfig> _configManager;
        public FeedbackHelper FeedbackbHelper { get; private set; }

        public SpecialtyParser SpecialtyParser { get; private set; }

        public ParserScheduler ReparseScheduler { get; private set; }

        public enum Dirs
        {
            Addons,
            AddonsCalc,
            Cache,
            Log,
            Feedback
        }

        public IReactor Core { get; private set; }
        public Task Initialize(string[] args)
        {
            return ReactorStartup.From(args, new ReactorStartupConfig { AwaitForConsoleInput = false }).Configure((with, core) =>
                {
                    _configManager = new ConfigManager<CalcConfig>(new ConfiguratorSettings(core, configFileName: "CalcConfig.json"));
                    Core = core;

                    core.Logger.UseConfig(new LoggerConfiguration { Component = core, LoggingAreas = LogArea.Other });
                    Core.DirectoryManager.AddDir(Dirs.Addons, "addons");
                    Core.DirectoryManager.AddDir(Dirs.Addons, Dirs.AddonsCalc, "calc");
                    Core.DirectoryManager.AddDir(Dirs.Cache, "cache");
                    Core.DirectoryManager.AddDir(Dirs.Feedback, "feedback");
                    with.Server("Calculator", int.Parse(args[0]), OnServerStart);

                }).RunAsync();
        }

        private static void CustomHeaderHandler(IServer server, IClient client, HttpRequest request)
        {
            var address = "";
            try
            {
                address = ((IPEndPoint)client.Socket.RemoteEndPoint).ToString();
            }
            catch
            {
                // ignored
            }

            server.Logger.TrySet(server.UserStats, request.Path, address,
                request.Headers.GetHeaderValueOrNull(BasicHeaders.UserAgent),
                request.Headers.GetHeaderValueOrNull(BasicHeaders.XID));
        }

        private void OnServerStart(IServer server)
        {
            server.SetApiControllers(typeof(CalcAPI));
            server.Config.CustomRequestPreProcessor = CustomHeaderHandler;
            FeedbackbHelper = new FeedbackHelper();
            SpecialtyParser = new SpecialtyParser();
            ReparseScheduler = new ParserScheduler(SpecialtyParser, Config);
        }


        public async Task LoadCache()
        {
            try
            {
                var file = Path.Combine(Core.DirectoryManager[Dirs.Cache], "all.json");
                if (File.Exists(file))
                {
                    using (var t = File.OpenText(file))
                    {
                        SpecialtyParser.LoadSpecialtyList(JsonConvert.DeserializeObject<List<SpecialtyInfo>>(await t.ReadToEndAsync()));
                    }
                }
            }
            catch (Exception ex) { Get.Core.Logger.NotifyError(LogArea.Other, ex); }
        }
        public async Task SaveCache()
        {
            try
            {
                var f = Path.Combine(Core.DirectoryManager[Dirs.Cache], "all.json");
                if (SpecialtyParser != null && SpecialtyParser.SpecialtyList.Count > 0)
                {
                    await File.WriteAllTextAsync(f, JsonConvert.SerializeObject(SpecialtyParser.SpecialtyList));
                }
            }
            catch (Exception ex) { Get.Core.Logger.NotifyError(LogArea.Other, ex); }
        }

    }

    internal class ParserScheduler : BaseScheduler
    {
        private SpecialtyParser Parser { get; }
        public ParserScheduler(SpecialtyParser parser, CalcConfig config) : base(config.UpdateDelay)
        {
            Parser = parser;
            CurrentWorkHandler = async () =>
            {
                if (Tools.CheckForInternetConnection())
                {
                    await Parser.ReloadTables();
                }
                SetDelay(config.UpdateDelay);
            };
        }
    }
}
