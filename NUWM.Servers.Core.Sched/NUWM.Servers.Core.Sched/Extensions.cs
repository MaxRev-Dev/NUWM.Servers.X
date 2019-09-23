using System.Globalization;
using MaxRev.Servers.Interfaces; 
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils;

namespace NUWM.Servers.Core.Sched
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
        /// <param name="useragent">UserAgent</param>
        /// <param name="xid">header x-id as email or unique user id</param>
        public static bool LogWrite(this ILogger logger, UserStats stats, string address, string request, string useragent, string xid)
        {
            string usrx;
            address ??= "";
            if (!string.IsNullOrEmpty(xid))
            {
                var t = UserStats.Parse(xid);
                usrx = $"\nLoginedAs: {t.UserType} [{(int)t.UserType}]  ID: {t.UserId}";
                stats.CheckUser(useragent, xid, t.UserId);
            }
            else usrx = " No user ID found";
            if (address.Contains(':')) address = address.Substring(0, address.IndexOf(':'));
            var d = TimeChron.GetRealTime().ToString("hh:mm:ss - dd.MM.yyyy", CultureInfo.CreateSpecificCulture("en-US"));
            if (request != null)
            {
                if (!request.Contains("ulog") &&
                    !request.Contains("trace"))
                {
                    if (useragent != null && !useragent.Contains("MaxRev") || useragent == null)
                    {
                        logger.Notify(LogArea.Http, LogType.Main,
                            $"\n{d}\nip: {address}{usrx}\nfrom: {useragent ?? "Unknown client"}\nreq={request}\n");
                    }
                }
            }

            return true;
        }
    }

}