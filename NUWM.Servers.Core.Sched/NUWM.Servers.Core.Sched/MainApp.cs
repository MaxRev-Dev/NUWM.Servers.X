using System;
using System.Net;
using System.Threading.Tasks;
using MaxRev.Servers;
using MaxRev.Servers.Core.Http;
using MaxRev.Servers.Interfaces;
using MaxRev.Utils.Http;

namespace NUWM.Servers.Core.Sched
{
    internal sealed class MainApp
    {
        public static MainApp Current => app ??= new MainApp();
        public enum Dirs { Addons, Subject_Parser }
        private static MainApp app;
        private SubjectParser _subjectParser;

        internal IReactor Core { get; private set; }

        public Task Initialize(string[] args)
        {
            var f = args?[0];
            if (string.IsNullOrEmpty(f))
            {
                Console.WriteLine("You must specify port");
                Environment.Exit(-1);
            }
            var port = -1;
            if (int.TryParse(f, out var portx))
                port = portx;
            return ReactorStartup.From(args).Configure((with, core) =>
            {
                Core = core;
                core.DirectoryManager
                    .GetFor<Dirs>(MaxRev.Servers.Utils.Filesystem.Dirs.WorkDir)
                    .AddDir(Dirs.Addons, "addons");
                core.DirectoryManager
                    .GetFor<Dirs>(MaxRev.Servers.Utils.Filesystem.Dirs.WorkDir)
                    .AddDir(Dirs.Addons, Dirs.Subject_Parser, "subjects_parser");

                //var servers = LoadBalancer.GetServers(Core, "Schedule", 2, (serv, isMain) =>
                // {
                //     serv.SetApiControllers(typeof(APIUtilty.API));
                //     if (serv.Config.Main != null)
                //         serv.Config.Main.ServerTypeName = new KeyValuePair<string, string>("X-NS-Type", "Schedule");

                //     if (isMain)
                //     {
                //         serv.EventMaster.ServerStarting += OnServerStart;
                //         return serv;
                //     }

                //     return serv;
                // });

                var server = core.GetServer("Schedule", port);
                server.SetApiControllers(typeof(API));
                server.CustomRequestPreProcessor = CustomHeaderHandler;
                server.EventMaster.ServerStarting += (s, e) =>
                    _subjectParser = new SubjectParser();

            }).RunAsync();
        }

        private static void CustomHeaderHandler(IClient client, HttpRequest request)
        {
            var address = "";
            try
            {
                address = request.Headers.GetHeaderValueOrNull(BasicHeaders.XFromIP) ??
                          ((IPEndPoint)client.Socket.RemoteEndPoint).ToString();
            }
            catch
            {
                // ignored
            }

            client.Server.Logger.TrySet(client.Server.UserStats, request.Path, address,
                request.Headers.GetHeaderValueOrNull(BasicHeaders.UserAgent),
                request.Headers.GetHeaderValueOrNull(BasicHeaders.XID));
        }
    }
}