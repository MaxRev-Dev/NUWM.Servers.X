using MaxRev.Servers;
using System;
using System.Net;
using MaxRev.Servers.Interfaces;
using MaxRev.Utils.Http;
using MaxRev.Servers.Core.Http;

namespace NUWM.Servers.Core.Sched
{
    internal sealed class Program
    {
        private static void Main(string[] args)
        {
            MainApp.Current.Initialize(args);
        }
    }

    internal sealed class MainApp
    {
        public static MainApp Current => app ?? (app = new MainApp());
        public enum Dirs { Addons, Subject_Parser }
        private static MainApp app;
        private SubjectParser subjectParser;

        internal IReactor Core { get; private set; }
        public void Initialize(string[] args)
        {
            var f = args?[0];
            if (string.IsNullOrEmpty(f))
            {
                Console.WriteLine("You must specify port");
                Environment.Exit(-1);
            }
            int port = -1;
            if (int.TryParse(f, out var portx))
                port = portx;
            ReactorStartup.From(args).Configure((with, core) =>
            {
                Core = core;
                core.DirectoryManager.AddDir(Dirs.Addons, "addons");
                core.DirectoryManager.AddDir(Dirs.Addons, Dirs.Subject_Parser, "subjects_parser");

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
                server.Config.CustomRequestPreProcessor = CustomHeaderHandler;
                server.EventMaster.ServerStarting += OnServerStart;

            }).Run();
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





        private void OnServerStart(IServer server, object args)
        {
            subjectParser = new SubjectParser();
            // new Thread(new ThreadStart(new SubjectParser.AutoReplaceHelper().Run)).Start();
        }
        public static void Restart()
        {
            Current.Core.Restart();
        }

    }
}
