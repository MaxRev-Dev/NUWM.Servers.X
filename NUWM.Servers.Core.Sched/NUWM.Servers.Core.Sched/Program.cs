using System.Threading.Tasks;

namespace NUWM.Servers.Core.Sched
{
    internal sealed class Program
    {
        private static Task Main(string[] args)
        {
            return MainApp.Current.Initialize(args);
        }
    }
}
