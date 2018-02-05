using System;

namespace NUWM.Servers.News
{
    using HelperUtilties;
    using Server;
    using System.Diagnostics;
    using System.IO;

    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Console.Clear();
            Server.UpTime = new System.Timers.Timer(1000);

            Server.UpTime.Start();
            Console.Title = "NUWM - News Server";

            try
            {
                try
                {
                    if (args != null && args.Length > 0) new Server(Convert.ToInt16(args[0]));
                    else new Server(3004);
                }
                catch (Exception) { new Server(3004); }
            }
            catch (Exception ex)
            {
                Rewave(ex);
            }
        }

        private static void Rewave(Exception ex)
        {
            StreamWriter file = File.CreateText("./log_" + TimeChron.GetRealTime().ToLongDateString().Replace(' ', '_').Replace(',', '_') + ".txt");
            file.WriteLine("");
            file.WriteLine(ex.Message);
            file.WriteLine(ex.StackTrace);
            file.WriteLine(ex.InnerException);
            file.WriteLine("");
            file.Close();
            Process.Start(new ProcessStartInfo("dotnet", "NUWM.Servers.News.dll 3004")
            {
                WorkingDirectory = "/home/tea/NUWM.Servers.X/NUWM.Servers.News"
            });
            Console.WriteLine("Started Successfully");
            Environment.Exit(0);
            Console.ReadLine();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //Rewave(e.ExceptionObject as Exception);
        }

        static void OnProcessExit(object sender, EventArgs e)
        {

        }
    }
}




