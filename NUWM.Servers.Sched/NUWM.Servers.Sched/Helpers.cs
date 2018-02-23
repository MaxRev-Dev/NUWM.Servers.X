using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;

namespace HelperUtilties
{
    using Server;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    class TimeChron
    {
        public static TimeSpan Offset { get; private set; }
        public static TimeSpan GetServerTimeDifference()
        {
            return GetServerTime() - DateTime.Now;
        }
        public static DateTime GetRealTime()
        {
            return DateTime.Now + Offset;
        }
        public static DateTime GetServerTime()
        {
            //default Windows time server
            const string ntpServer = "time.windows.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //The UDP port number assigned to NTP is 123
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //NTP uses UDP

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);

                //Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 3000;

                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }

        static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }

        public class TimeSyncRelay
        {
            static System.Timers.Timer timer;
            static void Timer_Elapsed(object sender, ElapsedEventArgs e)
            {
                timer.Stop();
                Offset = GetServerTimeDifference();
                Schedule_Timer();
            }

            public static DateTime scheduledTime;
            public static void Schedule_Timer()
            {
                if (Offset == new TimeSpan())
                    Offset = GetServerTimeDifference();


                DateTime nowTime = GetRealTime();

                scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, 0, 0).AddMinutes(30);

                if (nowTime > scheduledTime)
                {
                    scheduledTime = scheduledTime.AddHours(12);
                }

                double tickTime = (scheduledTime - GetRealTime()).TotalMilliseconds;
                timer = new System.Timers.Timer(tickTime);
                timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
                timer.Start();
            }
        }
    }
    class LogScheduler
    {
        static int LogEpoch = 0;
        static System.Timers.Timer timer;
        static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            timer.Stop();
            LogManage();
            Schedule_Timer();
        }
        public static DateTime scheduledTime;

        public static void LogManage()
        {
            try
            {
                var files = Directory.EnumerateFiles("./log");
                foreach (var o in files)
                {
                    var f = o.IndexOf("clientLog_") + "clientLog_".Length;
                    var t = o.Substring(0, f);
                    var date = o.Replace(t, "").Replace(".txt", "").Replace("-", ":");
                    var ts = TimeSpan.FromTicks(long.Parse(date));
                    var gd = TimeSpan.FromTicks(DateTime.Now.Ticks) - ts;
                    if (gd > new TimeSpan(7, 0, 0, 0))
                    {
                        File.Delete(o);
                    }
                }
                var ci = CultureInfo.CreateSpecificCulture("uk-UA");
                var d = DateTime.Now;
                if (Server.log.Count > 0)
                {
                    var ds = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                    var gg = File.CreateText("./log/clientLog_" + d.Ticks + ".txt");
                    gg.WriteLine("Log #{0} from start of session\nUptime: {1}",++LogEpoch,
                        String.Format("\nServer uptime: {0}d {1}h {2}m {3}s", ds.Days, ds.Hours, ds.Minutes, ds.Seconds));
                    gg.WriteLine("RAM memory size: " + (Process.GetCurrentProcess().WorkingSet64 / 1048576).ToString() + "mb\n");
                    gg.WriteLine(string.Format("Unique users in session: {0}", Server.UserStats.Current.UniqueUsers()));
                    gg.WriteLine(string.Format("Unique users in last hour: {0}", Server.UserStats.Current.UniqueUsersInHour()));
                    foreach (var i in Server.log)
                        gg.WriteLine(System.Net.WebUtility.UrlDecode(i));
                    Server.log.Clear();
                    gg.Close();
                }
                SubjectParser.AutoReplaceHelper.ManageAutoReplace();
                Server.UserStats.Current.DeleteStats();
            }
            catch (Exception ex)
            {
                Server.Errors.Add(ex);
            }
        }


        public static void Schedule_Timer()
        {

            DateTime nowTime = DateTime.Now;
            // scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day,nowTime.Hour,nowTime.Minute, 0, 0).AddMinutes(1);
            scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, 0, 0, 0).AddHours(1);

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


    public class CreateClientRequest
    {
        HttpClient cl = new HttpClient();
        string requestUri;
        HttpResponseMessage responseMessage;
        public CreateClientRequest(string uri)
        {
            HttpRequestHeaders headers = cl.DefaultRequestHeaders;
            headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("utf-8"));
            headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("uk"));
            headers.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("NUWM.Servers.Schedule", "1.0")));

            requestUri = uri;
        }

        public async Task<HttpResponseMessage> GetAsync()
        {
            try
            {
                responseMessage = await cl.GetAsync(requestUri);
                responseMessage.EnsureSuccessStatusCode();
                return responseMessage;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public async Task<HttpResponseMessage> PostAsync(HttpContent content)
        {
            try
            {
                responseMessage = await cl.PostAsync(requestUri, content); 
                responseMessage.EnsureSuccessStatusCode();
                return responseMessage;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

}
