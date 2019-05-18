using System.Globalization;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Utils;

namespace NUWM.Servers.Core.Calc
{
    internal static class LoggerExtensions
    {
        public static bool TrySet(this ILogger logger, UserStats stats, string request, string ip, string useragent, string xid)
        {
            return logger.Filter(request) && LogWrite(logger, stats, ip, request, useragent, xid);
        }

        /// <summary>
        /// Writes user state and request to log
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="stats"></param>
        /// <param name="address">IP adress of user</param>
        /// <param name="request">Request string</param>
        /// <param name="user">UserAgent</param>
        /// <param name="xid">header x-id as email or unique user id</param>
        public static bool LogWrite(this ILogger logger, UserStats stats, string address, string request, string user, string xid)
        {
            string usrx;
            address = address ?? "";
            if (!string.IsNullOrEmpty(xid))
            {
                var t = UserStats.Parse(xid);
                usrx = "\nLoginedAs: " + t.UserType + " [" + (int)t.UserType + "]" + "  ID: " + t.UserId;
                stats.CheckUser(user, xid, t.UserId);
            }
            else usrx = " No user ID found";
            if (address.Contains(':')) address = address.Substring(0, address.IndexOf(':'));
            var d = TimeChron.GetRealTime().ToString("hh:mm:ss - dd.MM.yyyy", CultureInfo.CreateSpecificCulture("en-US"));
            if (!request.Contains("ulog") && !request.Contains("trace") && !user.Contains("MaxRev"))
                logger.Notify(LogArea.Http, LogType.Main, "\n" + d + "\nip: " + address + usrx + "\nfrom: " + user + "\nreq=" + request + "\n");
            return true;
        }
    }

}