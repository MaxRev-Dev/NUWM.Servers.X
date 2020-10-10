using System;
using MaxRev.Servers;
using MaxRev.Servers.Configuration;
using MaxRev.Servers.Core.Http;
using MaxRev.Servers.Interfaces;
using MaxRev.Utils.Http;
using Microsoft.Extensions.DependencyInjection;
using NUWM.Servers.Core.Calc.Config;
using NUWM.Servers.Core.Calc.Extensions;
using NUWM.Servers.Core.Calc.Services;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MaxRev.Servers.Utils.Filesystem;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils;
using NUWM.Servers.Core.Calc.Services.Parsers;

namespace NUWM.Servers.Core.Calc
{
    public sealed class App
    {
        private static App _app;
        public static App Get => _app ??= new App();

        private ConfigManager<CalcConfig> _configManager;

        public enum Directories
        {
            Addons,
            AddonsCalc,
            Cache,
            Log,
            Feedback
        }

        public Task Initialize(string[] args)
        { 
#if DEBUG
            Console.OutputEncoding = Encoding.UTF8;
#endif
            return ReactorStartup
                .From(args, new ReactorStartupConfig { AwaitForConsoleInput = false, AutoregisterControllers = true })
                .Configure((with, core) =>
                {
                    var dm = core.DirectoryManager.SwitchTo<Directories>();
                    core.Logger.UseConfig(new LoggerConfiguration
                    {
                        Component = core,
                        LoggingAreas = LogArea.Other,
#if DEBUG
                        WriteToConsole = true
#endif
                    });
                    dm.AddDir(Directories.Addons, "addons");
                    dm.AddDir(Directories.Addons, Directories.AddonsCalc, "calc");
                    dm.AddDir(Directories.Cache, "cache");
                    dm.AddDir(Directories.Feedback, "feedback");
                    with.Services(x =>
                    {
                        x.AddSingleton(_ => dm);
                        x.AddSingleton(_ => _configManager.ConfigInstance);
                        x.AddSingleton<CacheHelper>();
                        x.AddSingleton<FetchService>();
                        x.AddSingleton<SpecialtyParser>();
                        x.AddSingleton<FeedbackHelper>();
                        x.AddSingleton<ParserScheduler>();
                        x.AddTransient<Calculator>();
                    });

                    with.Server("Calculator", int.Parse(args[0]), OnServerStart);
                    with.FinalizingStartup += FinalizingStartup;
                }).RunAsync();
        }

        private void FinalizingStartup(IReactor core)
        {
            _configManager = new ConfigManager<CalcConfig>(new ConfiguratorSettings(core, configPath: core.DirectoryManager[Dirs.WorkDir],
                configFileName: "CalcConfig.json"));

            var parser = core.Services.GetService<SpecialtyParser>();
            core.Services.GetService<CacheHelper>();
            parser.RunAsync();
        }

        private void OnServerStart(IServer server)
        {
            server.Features.ReplaceFeature<IRequestPreProcessor>(() => new HeaderHandler());
        }

        class HeaderHandler : IRequestPreProcessor
        {
            public void Process(IClient client, HttpRequest request)
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

                var server = client.Server;

                server.Logger.TrySet(server.Features.GetFeature<UserStats>(), request.Path, address,
                    request.Headers.GetHeaderValueOrNull(BasicHeaders.UserAgent),
                    request.Headers.GetHeaderValueOrNull(BasicHeaders.XID));
            }
        }
    }
}