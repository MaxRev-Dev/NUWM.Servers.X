using MR.Servers;
using System;
using System.Collections.Generic;
using static MR.Servers.Core.Proxy.Bridge;

namespace NUWM.Servers.Bridge
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args != null && args.Length == 2)
            {
                Reactor Core = new Reactor();

                Core.Config.Main.ServerTypeName = new KeyValuePair<string, string>("x-NS-type", "Bridge");
                new MR.Servers.Core.Proxy.Bridge(Core).
                   UnavailableHandler(sender =>
                   {
                       string error = "It's NUWM.Servers.Bridge response. One of NUWM.Servers is anavailable now";
                       sender.Send(System.Net.HttpStatusCode.OK,
                           "{\"code\":" + ((int)StatusCode.ServerNotResponsing).ToString() +
                           ",\"cache\":false,\"error\":\"" + error + "\",\"response\":null}");

                   }).Link(
                        Convert.ToInt16(args[0]), //source client
                        Convert.ToInt16(args[1])); //localhost dest client
            }
            else
            {
                Environment.Exit(0);
            }
        }
    }
}
