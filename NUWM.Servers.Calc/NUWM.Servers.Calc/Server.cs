using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    using HelperUtilties;
    using System.Globalization;
    using static global::Server.AutorizationManager;
    using static JSON.SpecialtiesVisualiser;

    class AutorizationManager
    {
        private Dictionary<string, string> USERS;
        public static List<string> USessions;

        public AutorizationManager()
        {
            InitUsersDB();
            USessions = new List<string>();
        }
        public enum LoginStatus
        {
            OK, NonAthorized, PassError, NotImplemented, Default, N401
        }
        public LoginStatus CheckUser(string name, string pass)
        {
            LoginStatus st;
            if (USERS.ContainsKey(name))
                if (USERS[name] == pass)
                    st = LoginStatus.OK;
                else st = LoginStatus.PassError;
            else st = LoginStatus.NonAthorized;
            return st;
        }
        private async void InitUsersDB()
        {
            if (!File.Exists("./addons/users.txt")) return;
            StreamReader f = File.OpenText("./addons/users.txt");
            string direct = await f.ReadToEndAsync();
            string[] lines = direct.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            USERS = new Dictionary<string, string>();
            foreach (var i in lines)
            {
                var t = i.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                USERS.Add(t[0], t[1]);
            }
        }
        public string GetPageAsAuthentica(LoginStatus status)
        {
            var f = "./www/admin/index.html";
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.Load(f);
            switch (status)
            {
                case LoginStatus.OK:
                    { return File.ReadAllText("./www/admin/console/index.html"); }
                case LoginStatus.Default:
                    {
                        return File.ReadAllText(f);
                    }
                case LoginStatus.N401:
                    {
                        string CodeStr = 403.ToString() + " " + ((HttpStatusCode)403).ToString();
                        return "<html><body><h1>" + CodeStr + "</h1></body></html>";
                    }
                case LoginStatus.NonAthorized:
                    {
                        HtmlAgilityPack.HtmlNode node = new HtmlAgilityPack.HtmlNode(HtmlAgilityPack.HtmlNodeType.Element, doc, 0)
                        {
                            Name = "div"
                        };
                        node.AddClass("invalid-feedback");
                        node.InnerHtml = "Невірний логін або пароль";
                        doc.GetElementbyId("l1").AppendChild(node);
                        doc.GetElementbyId("name").AddClass("is-invalid");
                        return doc.DocumentNode.OuterHtml;
                    }
                case LoginStatus.PassError:
                    {
                        HtmlAgilityPack.HtmlNode node = new HtmlAgilityPack.HtmlNode(HtmlAgilityPack.HtmlNodeType.Element, doc, 0)
                        {
                            Name = "div"
                        };
                        node.AddClass("invalid-feedback");
                        node.InnerHtml = "Невірний пароль";
                        doc.GetElementbyId("l2").AppendChild(node);
                        doc.GetElementbyId("pass").AddClass("is-invalid");
                        return doc.DocumentNode.OuterHtml;
                    }
                case LoginStatus.NotImplemented:
                    {
                        doc.GetElementbyId("non-auto").RemoveClass("fade");
                        return doc.DocumentNode.OuterHtml;
                    }
                default:
                    return null;
            }

        }
        public bool CheckSession(string v)
        {
            return USessions.Contains(v);
        }

        public void RemoveSession(string v)
        {
            USessions.Remove(v);
        }

        public string AddSesion(string v)
        {
            USessions.Add(v);
            return v;
        }
    }

    class Server
    {
        public static int AllUsersCount = 0;
        public static Specialty.SpecialtyParser CurrentParser;
        public static AutorizationManager AManager;

        public static List<Exception> Errors;
        public static string[] dirs = new[] {
                "./addons",
                "./addons/calc",
                "./log"
        };
        private void CheckDirs()
        {
            foreach (var item in dirs)
            {
                if (!Directory.Exists(item)) Directory.CreateDirectory(item);
            }
        }
        public class UserStats
        {
            public static UserStats Current;
            private Dictionary<string, StatsInfo> UstatsIP;
            public UserStats()
            {
                UstatsIP = new Dictionary<string, StatsInfo>();
            }
            public string UniqueUsers()
            {
                return (Server.AllUsersCount + UstatsIP.Keys.Count).ToString();
            }
            public string UniqueUsersInHour()
            {
                return (UstatsIP.Keys.Count).ToString();
            }
            public void CheckUser(string ip, string request, string user)
            {
                if (!UstatsIP.Keys.Contains(ip))
                {
                    UstatsIP.Add(ip, new StatsInfo()
                    {
                        RequestsCount = 1
                    });
                }
                else
                {
                    UstatsIP[ip].RequestsCount++;
                }
                if (!UstatsIP[ip].Devices.Contains(user)) UstatsIP[ip].Devices.Add(user);
                UstatsIP[ip].Requests.Add(DateTime.Now.ToString("hh:mm:ss - dd.MM.yyyy") + " => " + request);
            }
            public void DeleteStats()
            {
                Server.AllUsersCount += UstatsIP.Keys.Count;
                UstatsIP.Clear();
            }
            public class StatsInfo
            {
                public List<string> Devices = new List<string>();
                public int RequestsCount { get; set; }
                public List<string> Requests = new List<string>();
            }
        }
        public Server(int Port)
        {
            CheckDirs();
            Errors = new List<Exception>();
            UserStats.Current = new UserStats();
            Listener = new TcpListener(IPAddress.Any, Port);
            try { Listener.Start(); }
            catch (Exception)
            {
                Console.WriteLine("Server is active! No need to restart");
                Environment.Exit(-1);
            }

            #region MainThread
            CurrentParser = new Specialty.SpecialtyParser();
            new Thread(new ThreadStart(ParseAsync)).Start();
            #endregion

            ScheduleTask.Schedule_Timer();
            LogScheduler.Schedule_Timer();
            TimeChron.TimeSyncRelay.Schedule_Timer();

            AManager = new AutorizationManager();

            while (true)
            {
                try
                {
                    TcpClient Client = Listener.AcceptTcpClient();
                    new Thread(new ParameterizedThreadStart(ClientThread)).Start(Client);
                    ConnectionsClosedCount++;
                }
                catch (Exception e) { Errors.Add(e); }
            }
        }

        public static System.Timers.Timer UpTime;




        public static string CurrentState = "";
        public static int ConnectionsClosedCount = 0;
        TcpListener Listener;
        internal static int taskDelayH;


        static void ParseAsync() => new Task(new Action(() =>
        {
            CurrentParser.GetLastModulus();
        })).Start();
        static void ClientThread(Object StateInfo) => new Client((TcpClient)StateInfo);

        ~Server()
        {
            if (Listener != null)
                Listener.Stop();
        }
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (client.OpenRead("http://clients3.google.com/generate_204"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        public static List<string> log = new List<string>();
        public static void Log(string address, string request, string user)
        {
            if (address.Contains(':')) address = address.Substring(0, address.IndexOf(':'));
            if (address.Contains('.')) UserStats.Current.CheckUser(address, request, user);
            var d = DateTime.Now.ToString("hh:mm:ss - dd.MM.yyyy", CultureInfo.CreateSpecificCulture("en-US"));
            if (!request.Contains("ulog") && !request.Contains("trace") && !user.Contains("MaxRev"))
                log.Add("\n" + d + "\nip:" + address + "\nfrom:" + user + "\nreq=" + request + "\n");
        }
    }

    class Client
    {
        private void SendError(TcpClient Client, int Code)
        {
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            string Html = "<html><body><h1>" + CodeStr + "</h1></body></html>";
            string Str = "HTTP/1.1 " + CodeStr + "\nContent-type: text/html\nContent-Length:" + Html.Length.ToString() + "\n\n" + Html;
            byte[] Buffer = Encoding.ASCII.GetBytes(Str);
            try
            {
                Client.GetStream().Write(Buffer, 0, Buffer.Length);
                Client.Close();
            }
            catch (Exception) { }
        }
        TcpClient client;

        Dictionary<string, string> Headers = new Dictionary<string, string>();
        public Client(TcpClient Client)
        {
            client = Client;
            string Request = "", Content = "";
            byte[] Buffer = new byte[1024];
            int Count;
            while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
            {
                Request += Encoding.UTF8.GetString(Buffer, 0, Count);
                if (Request.IndexOf("\r\n\r\n") >= 0)
                {
                    string[] rx = Request.Split("\r\n\r\n", StringSplitOptions.None);
                    Request = rx[0];
                    if (rx.Count() > 1)
                        Content = rx[1];
                    break;
                }
            }
            try
            {
                foreach (var i in Request.Split('\n'))
                {
                    if (i.IndexOf(": ") == -1) continue;
                    var t = i.Substring(0, i.IndexOf(": "));
                    var s = i.Substring(i.IndexOf(": ") + 2);
                    Headers.Add(t.ToLower(), s);
                }
                if (Request.ToLower().Contains("content-length"))
                {
                    if (int.TryParse(Headers["content-length"], out int res))
                    {
                        int cont = Encoding.UTF8.GetBytes(Content).Length;
                        var ui = Encoding.UTF8.GetBytes(Content).Length;
                        if (ui != res)
                            while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
                            {
                                cont += Count;
                                Content += Encoding.UTF8.GetString(Buffer, 0, Count);
                                ui = Encoding.UTF8.GetBytes(Content).Length;
                                if (ui == res)
                                {
                                    break;
                                }
                            }
                    }
                }
            }
            catch (Exception) { }
            string ipus = "unknown";
            if (Request.ToLower().Contains("x-routed-by: maxrev.nuwm.server.bridge"))
            {
                if (Request.ToLower().Contains("x-from-ip"))
                    ipus = Headers["x-from-ip"];
                if (Content.Contains("\r\n\r\n"))
                {
                    Content = Content.Substring(Request.IndexOf("\r\n\r\n"));
                    Request = Content.Substring(0, Request.IndexOf("\r\n\r\n"));
                }
                else Request = Content;
                Headers.Clear();
                foreach (var i in Request.Split('\n'))
                {
                    if (i.IndexOf(": ") == -1) continue;
                    var t = i.Substring(0, i.IndexOf(": "));
                    var s = i.Substring(i.IndexOf(": ") + 2);
                    Headers.Add(t.ToLower(), s);
                }
            }
            Match ReqMatch = Regex.Match(Request, @"^\w+\s+([^\s\?]+)[^\s]*\s+HTTP/.*|");

            if (ReqMatch == Match.Empty)
            {
                SendError(Client, 400);
                return;
            }
            else
            {
                if (ReqMatch.Groups[0].Value != "")
                {
                    string RequestUri = ReqMatch.Groups[0].Value.Split(' ')[1];
                    try
                    {
                        string useragent = "unknown";
                        if (Request.ToLower().Contains("user-agent"))
                            useragent = Headers["user-agent"];
                        SetLog(RequestUri, ipus, useragent);
                    }
                    catch (Exception) { }
                    RequestUri = Uri.UnescapeDataString(RequestUri);
                    if (RequestUri.IndexOf("..") >= 0)
                    {
                        SendError(Client, 400);
                        return;
                    }
                    if (RequestUri == "") { SendError(Client, 400); return; }
                    var tmp = RequestUri.Substring(1);
                    string action = ""; Dictionary<string, string> query = new Dictionary<string, string>();

                    if (tmp.Contains("?"))
                    {
                        action = tmp.Substring(0, tmp.IndexOf('?'));

                        foreach (var kp in tmp.Substring(tmp.IndexOf('?') + 1).Split('&'))
                        {
                            try
                            {
                                if (!kp.Contains('=')) { query.Add(kp, ""); continue; }
                                var k_p = kp.Split('=');
                                query.Add(k_p[0], k_p[1]);
                            }
                            catch (ArgumentException)
                            {
                                SendError(Client, 501); return;
                            }
                            catch (InvalidOperationException)
                            {
                                SendError(Client, 501); return;
                            }
                            catch { }
                        }
                    }
                    else
                        action = tmp;


                    APIUtilty.API api = new APIUtilty.API(query);
                    if (action.StartsWith("api"))
                    {
                        try
                        {
                            var task = api.PrepareForResponse(Request, Content, action);
                            SendResponseStr(task.Item1, task.Item2, Buffer, Count, Client);
                        }
                        catch (Exception ex)
                        {
                            SendResponseStr(Newtonsoft.Json.JsonConvert.SerializeObject(
                                APIUtilty.API.ResponseTyper(ex)), "text/json", Buffer, Count, Client);
                        }
                        return;
                    }
                    else
                    {
                        string FilePath = null; string ContentType = "";
                        if (Request.Contains("/logout"))
                        {
                            try
                            {
                                foreach (var t in Headers["cookie"].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (t.StartsWith("mx_ses"))
                                    {
                                        Server.AManager.RemoveSession(t.Split("=")[1]);
                                        break;
                                    }
                                }
                            }
                            catch { }
                            SendResponseStr(Server.AManager.GetPageAsAuthentica(LoginUser(Headers, Content)), "text/html", Buffer, Count, Client, false, true);
                            return;
                        }
                        if (RequestUri.Contains("admin"))
                        {
                            if (Request.Contains("POST"))
                            {
                                var s = LoginUser(Headers, Content);
                                SendResponseStr(Server.AManager.GetPageAsAuthentica(s), "text/html", Buffer, Count, Client, s == LoginStatus.OK);
                                return;
                            }
                            bool autorized = false;
                            if (Headers.ContainsKey("cookie"))
                            {
                                if (Headers["cookie"].Contains("mx_ses"))
                                {
                                    foreach (var t in Headers["cookie"].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (t.StartsWith("mx_ses"))
                                        {
                                            if (Server.AManager.CheckSession(t.Split("=")[1]))
                                            {
                                                autorized = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            if (!autorized)
                            {
                                if (RequestUri.EndsWith('/'))
                                { SendResponseStr(Server.AManager.GetPageAsAuthentica(LoginStatus.Default), "text/html", Buffer, Count, Client); return; }

                                if (RequestUri.Contains("admin/console") && RequestUri.Substring(RequestUri.LastIndexOf('/')).Contains("."))
                                {
                                    SendResponseStr(Server.AManager.GetPageAsAuthentica(LoginStatus.N401), "text/html", Buffer, Count, Client); return;
                                }
                            }
                        }
                        if (RequestUri.EndsWith("/"))
                        {
                            RequestUri += "index.html";
                        }
                        else if (!RequestUri.Contains(".")) RequestUri += "/index.html";

                        FilePath = "./www/" + RequestUri;

                        if (!File.Exists(FilePath))
                        {
                            SendError(Client, 404);
                            return;
                        }
                        string Extension = RequestUri.Substring(RequestUri.LastIndexOf('.'));

                        switch (Extension)
                        {
                            case ".htm":
                            case ".html":
                                ContentType = "text/html";
                                break;
                            case ".css":
                                ContentType = "text/css";
                                break;
                            case ".js":
                                ContentType = "text/javascript";
                                break;
                            case ".jpg":
                                ContentType = "image/jpeg";
                                break;
                            case ".ico":
                                ContentType = "image/x-icon";
                                break;
                            case ".svg":
                                ContentType = "image/svg+xml";
                                break;
                            case ".jpeg":
                            case ".png":
                            case ".gif":
                                ContentType = "image/" + Extension.Substring(1);
                                break;
                            default:
                                if (Extension.Length > 1)
                                {
                                    ContentType = "application/" + Extension.Substring(1);
                                }
                                else
                                {
                                    ContentType = "application/unknown";
                                }
                                break;
                        }
                        SendResponse(GetFile(FilePath), ContentType, Buffer, Count, Client); return;
                    }
                }
                SendError(Client, 400);
            }
        }
        private FileStream GetFile(string FilePath)
        {
            FileStream FS;
            try
            {
                FS = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch { return null; }
            return FS;
        }

        private LoginStatus LoginUser(Dictionary<string, string> headers, string content)
        {
            LoginStatus st = LoginStatus.Default;
            var f = headers["referer"];
            if (f.Contains("/admin"))
            {
                try
                {
                    Dictionary<string, string> query = new Dictionary<string, string>();
                    foreach (var g in content.Split('&'))
                    {
                        var nnv = g.Split('=');
                        query.Add(nnv[0], nnv[1]);
                    }

                    st = Server.AManager.CheckUser(query["n"], query["p"]);
                }
                catch { st = LoginStatus.NonAthorized; }
            }
            else st = LoginStatus.NotImplemented;
            return st;
        }

        private void SetLog(string request, string ip, string useragent)
        {
            var op = client.Client.RemoteEndPoint;
            if (!request.EndsWith(".js") &&
                !request.EndsWith(".html") &&
                !request.EndsWith(".png") &&
                !request.EndsWith(".svg") &&
                !request.EndsWith(".css") &&
                !request.EndsWith(".ico"))
                Server.Log(ip, request, useragent);
        }


        private string GetHeaders(string contentType, long contentLength, bool SetCookie, bool DelBrowserCookie)
        {
            var cookie = (SetCookie ? "\nSet-Cookie: mx_ses=" + Server.AManager.AddSesion(DateTime.Now.ToLongTimeString().GetHashCode().ToString()) + "; HttpOnly" : "");
            if (string.IsNullOrEmpty(cookie))
                cookie = (DelBrowserCookie ? "\nSet-Cookie: mx_ses=; expires=Thu, 01 Jan 1970 00:00:01 GMT; HttpOnly" : "");
            return "HTTP/1.1 200 OK" +
                "\nContent-Type: " + contentType + ((contentType != "image/x-icon") ? "; charset=utf-8" : "") +
                "\nPragma: no-cache" +
                "\nAccess-Control-Allow-Origin: http://" + Headers["host"] +
                "\nAccess-Control-Allow-Methods:*" +
                "\nContent-Language: uk-UA" +
                    cookie +
                "\nX-Powered-By: NUWM.Servers.Calc by MaxRev\nX-NS-Type: Calculator" +
                "\nDate:" + TimeChron.GetRealTime().ToString("hh:mm:ss - dd.MM.yyyy") + 
                "\nContent-Length: " + contentLength + "\n\n";
        }

        private void SendResponse(FileStream FS, string ContentType, byte[] Buffer, int Count, TcpClient Client, bool SetCookie = false, bool DelBrowserCookie = false)
        {
            if (FS == null) SendError(Client, 500);
            try
            {
                byte[] HeadersBuffer = Encoding.ASCII.GetBytes(GetHeaders(ContentType, FS.Length, SetCookie, DelBrowserCookie));
                Client.GetStream().Write(HeadersBuffer, 0, HeadersBuffer.Length);

                while (FS.Position < FS.Length)
                {
                    Count = FS.Read(Buffer, 0, Buffer.Length);
                    Client.GetStream().Write(Buffer, 0, Count);
                }

                FS.Close();
                Client.Close();
            }
            catch (Exception) { }
        }

        private void SendResponseStr(string FS, string ContentType, byte[] Buffer, int Count, TcpClient Client, bool SetCookie = false, bool DelBrowserCookie = false)
        {
            try
            {
                byte[] utf8buffer = Encoding.GetEncoding(65001).GetBytes(Uri.UnescapeDataString(FS));

                byte[] HeadersBuffer = Encoding.UTF8.GetBytes(GetHeaders(ContentType, utf8buffer.Length, SetCookie, DelBrowserCookie));
                Client.GetStream().Write(HeadersBuffer, 0, HeadersBuffer.Length);

                Stream stream = new MemoryStream(utf8buffer);

                int read;
                while ((read = stream.Read(utf8buffer, 0, utf8buffer.Length)) > 0)
                {
                    Client.GetStream().Write(utf8buffer, 0, read);
                }
                stream.Close();
                Client.Close();
            }
            catch (Exception) { }
        }
    }

}