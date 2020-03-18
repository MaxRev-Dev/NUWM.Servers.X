using System;
using System.Threading.Tasks;

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
}
