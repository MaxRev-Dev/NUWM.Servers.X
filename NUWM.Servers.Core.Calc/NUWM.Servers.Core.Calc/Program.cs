using HelperUtilties;
using JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static JSON.SpecialtiesVisualiser;

namespace NUWM.Servers.Core.Calc
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
    sealed class MainApp
    {
        internal static int taskDelayH;
        public static int AllUsersCount = 0;
        public static MainApp Current;
        public static SpecialtiesVisualiser.Specialty.SpecialtyParser CurrentParser;
        public static string[] dirs = new[] {
                "./addons",
                "./addons/calc",
                "./cache",
                "./log",
                "./feedback"
        };
        private static int port;
        public MainApp() { Current = this; }
        public void Initialize(int port)
        {
            MainApp.port = port;
            Core.Reactor.CheckDirs(dirs);
            new Core.Reactor(port, "Calculator", new UnhandledExceptionEventHandler(OnUnhandledException));
            Core.Reactor.Current.SetAPIHandler(new Core.Reactor.Client.ApiHandler(HandlerForApi));
            Core.Reactor.Current.SetStartFinilizeHandler(new Core.Reactor.Server.StartFinalizingHandler(OnServerStart));
            Core.Reactor.Current.SetStatsInvokers(new Core.Reactor.StatsInvoker(GetServerVersion), new Core.Reactor.StatsInvoker(GetReqsCount));
            Core.Reactor.Current.RunServerOnPort();
        }
        string GetReqsCount() => APIUtilty.API.ApiRequestCount.ToString();
        string GetServerVersion() => GetVersion().ToString();
        void OnServerStart()
        {
            new APIUtilty.FeedbackHelper();
            CurrentParser = new Specialty.SpecialtyParser();
            ScheduleTask.Schedule_Timer();
            new Thread(new ThreadStart(() =>
            {
                new Task(new Action(() =>
                    CurrentParser.GetLastModulus())).Start();
            })).Start();
        }
        public static void Restart()
        {
            new Task(new Action(() =>
            {
                Reactor.Server.Current.StopListen();
                Task.Delay(1000 * 65).Wait();
                Process.Start(new ProcessStartInfo("dotnet", string.Format("NUWM.Servers.Core.Calc.dll {0}", port)));
                Environment.Exit(0);
            })).Start();

        }
        async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
            await SaveCache();

        public static Version GetVersion() => System.Reflection.Assembly.GetAssembly(typeof(MainApp)).GetName().Version;
        public async Task LoadCache()
        {
            try
            {
                var f = "./cache/all.txt";
                if (File.Exists(f))
                {
                    var t = File.OpenText(f);
                    CurrentParser.res = JsonConvert.DeserializeObject<List<Specialty>>(await t.ReadToEndAsync());
                }
            }
            catch (Exception ex) { Logger.Current.Errors.Add(ex); }
        }
        public async Task SaveCache()
        {
            try
            {
                var f = "./cache/all.txt";
                if (CurrentParser != null && CurrentParser.res.Count > 0)
                    await File.WriteAllTextAsync(f, JsonConvert.SerializeObject(CurrentParser.res));
            }
            catch (Exception ex) { Logger.Current.Errors.Add(ex); }
        }
        Tuple<int, string, string> HandlerForApi(Core.Reactor.Client.ClientInfo obj)
        {
            string content = "", type = "text/json";
            try
            {
#if DEBUG
                Console.WriteLine("\n\nClient API handler start");
#endif
                APIUtilty.API api = new APIUtilty.API(obj.Query, obj.Headers);

                try
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    source.CancelAfter(10 * 1000);
                    var token = source.Token;
                    var task = api.PrepareForResponse(obj.Request, obj.Content, obj.Action);
                    while (!task.IsCompleted)
                    {
                        if (token.IsCancellationRequested)
                        {
                            content = JsonConvert.SerializeObject(new Response()
                            {
                                Code = StatusCode.ServerSideError,
                                Error = "Error with async method",
                                Content = null
                            });
                        }
                        Task.Delay(10);
                    }
                    if (task.Result == null)
                        return new Tuple<int, string, string>(501, null, null);
                    else
                    {
                        content = task.Result.Item1;
                        type = task.Result.Item2;
                    }
                }
                catch (Exception ex)
                {
                    content = JsonConvert.SerializeObject(APIUtilty.API.ResponseTyper(ex));
                }
            }
            catch (Exception ex)
            {
                content = APIUtilty.API.CreateErrorResp(ex);
            }
            return new Tuple<int, string, string>(200, content, type);
        }
    }


    class ScheduleTask
    {
        static System.Timers.Timer timer;
        static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            timer.Stop();
            if (NUWM.Servers.Core.Reactor.Server.CheckForInternetConnection()) MainApp.CurrentParser.GetLastModulus();
            Schedule_Timer();
        }
        public static DateTime scheduledTime;
        public static void Schedule_Timer()
        {

            DateTime nowTime = DateTime.Now;
            // scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day,nowTime.Hour,nowTime.Minute, 0, 0).AddMinutes(1);
            scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, 0, 0, 0, 0).AddHours(12);

            if (nowTime > scheduledTime)
            {
                scheduledTime = scheduledTime.AddHours(12);
            }

            double tickTime = (scheduledTime - DateTime.Now).TotalMilliseconds;
            timer = new System.Timers.Timer(tickTime);
            timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
            timer.Start();
        }
    }

}
