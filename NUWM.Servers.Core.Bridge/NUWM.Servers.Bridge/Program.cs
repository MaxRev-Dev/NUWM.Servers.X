
using System;
using System.Threading.Tasks;
using MaxRev.Servers;
using MaxRev.Servers.API.Response;
using MaxRev.Servers.Interfaces;

namespace NUWM.Servers.Bridge
{
    internal class Program
    {
        private static Task Main(string[] args)
        {
            if (args != null && args.Length == 2)
            {
                //if (Core.Config.Main != null)
                //    Core.Config.B = new KeyValuePair<string, string>("x-NS-type", "Bridge");

                return ReactorStartup.From(args, new ReactorStartupConfig{AwaitForConsoleInput = false})
                    .Configure((with, core) =>
                {
                    with.Bridge(out IBridgeServer server);
                    server.UnavailableHandler(sender =>
                    {
                        string error = "It's NUWM.Servers.Bridge response. One of NUWM.Servers is anavailable now";
                        sender.SendFromCode(System.Net.HttpStatusCode.OK, new JsonResponseContainer
                        {
                            Code = StatusCode.ServerNotResponding,
                            Error = new object[] { error }
                        }, "application/json");
                    }).Link(
                        Convert.ToInt16(args[0]), //source client
                        Convert.ToInt16(args[1])); //localhost dest client

                    //var b = with.CreateBridgeServerBuilder();
                    //b.OnUnavailable(async sender =>
                    //{
                    //    string error = "It's NUWM.Servers.Bridge response. One of NUWM.Servers is anavailable now";
                    //    await sender.SendFromCodeAsync(System.Net.HttpStatusCode.OK, new JsonResponseContainer
                    //    {
                    //        Code = StatusCode.ServerNotResponding,
                    //        Error = new object[] { error }
                    //    }, "application/json");
                    //});
                    //b.Link(
                    //    Convert.ToInt16(args[0]), //source client
                    //    Convert.ToInt16(args[1])); //localhost dest client
                    //with.Bridge(b);
                }).RunAsync();
            }

            return Task.CompletedTask;
        }
    }
}
