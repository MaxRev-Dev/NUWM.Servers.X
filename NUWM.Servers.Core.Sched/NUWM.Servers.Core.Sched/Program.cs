using JSON;
using MR.Servers;
using MR.Servers.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static MR.Servers.Client;

namespace NUWM.Servers.Sched
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
            new MainApp().Initialize(int.Parse(f));
        }
    }

    internal sealed class MainApp
    {
        public static string[] dirs = new[] {
                "./addons",
                "./addons/subjects_parser",
                "./log"
        };
        private static int port;
        public CancellationTokenSource Cancellation { get; private set; }
        public static MainApp Current;
        public enum Dirs { Addons, Subject_Parser}
        public Reactor Core { get; internal set; }
        public void Initialize(int port)
        {
            Current = this;
            MainApp.port = port;

            Cancellation = new CancellationTokenSource();
             Core = new Reactor();
            var bal = Core.GetBalancer(port);

            Core.DirectoryManager.AddDir(Dirs.Addons, "addons");
            Core.DirectoryManager.AddDir(Dirs.Addons, Dirs.Subject_Parser, "subjects_parser");

            var servers = LoadBalancer.GetServers(Core, "Schedule", 2, (serv, isMain) =>
             {
                 serv.SetApiController(typeof(APIUtilty.API));

                 serv.Config.Main.ServerTypeName = new KeyValuePair<string, string>("X-NS-Type", "Schedule");

                 if (isMain)
                 {
                     serv.EventMaster.ServerStarting += OnServerStart;
                     return serv;
                 } 
                 
                 return serv;
             }); 

             
             

            Core.Listen(servers).Wait(Cancellation.Token);
        }
         
         
        
         
        

        private void OnServerStart(object sender, object args)
        {
            new SubjectParser.SubjectParser();
           // new Thread(new ThreadStart(new SubjectParser.AutoReplaceHelper().Run)).Start();
        }
        public static Version GetVersion()
        {
            return System.Reflection.Assembly.GetAssembly(typeof(MainApp)).GetName().Version;
        }

        public static void Restart()
        {
            new Task(new Action(() =>
            {
                MainApp.Current.Cancellation.Cancel();
                Task.Delay(1000 * 65).Wait();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("dotnet", string.Format("NUWM.Servers.Sched.dll {0}", port)));
                Environment.Exit(0);
            })).Start();

        }
         
    }
}
