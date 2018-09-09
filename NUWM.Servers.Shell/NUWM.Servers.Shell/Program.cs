using System;
using System.Diagnostics;

namespace NUWM.Servers.Shell
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = "/home/tea/NUWM.Servers", 
                name= "NUWM.Servers.dll",
                port = "3000",
            port2 = "";
            if (args != null && args.Length > 0)
            {
                path = args[0];
                if (args.Length == 2 && int.TryParse(args[1], out int portp))
                {
                    port = args[1];
                }
                else if (args.Length == 3)
                {
                    name = args[1];
                    port = args[2];
                }
                else if (args.Length == 4)
                {
                    name = args[1];
                    port = args[2];
                    port2 = args[3];
                }
            }
            try
            {
                Process pr = Process.Start(new ProcessStartInfo("dotnet", String.Format("{0} {1} {2}", name, port, port2))
                {
                    WorkingDirectory = path
                });
            Console.WriteLine("Started {0} at port {1} Successfully",name,port);
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            
            Environment.Exit(0);
        }
    }
}
