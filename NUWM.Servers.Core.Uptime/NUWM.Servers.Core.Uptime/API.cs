using HelperUtilties;
using JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace NUWM.Servers.Core.Uptime
{
    namespace APIUtilty
    {
        [Serializable]
        public sealed class API
        {
            public static long ApiRequestCount = 0;
            Dictionary<string, string> query, headers;
            public API(Dictionary<string, string> query, Dictionary<string, string> headers)
            {
                this.headers = headers;
                this.query = query;
            }
            public Dictionary<string, string> Query { get { return query; } set { query = value; } }
            /// <summary>
            /// Calculates the lenght in bytes of an object 
            /// and returns the size 
            /// </summary>
            /// <param name="TestObject"></param>
            /// <returns></returns>
            private int GetObjectSize(object TestObject)
            {
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                byte[] Array;
                bf.Serialize(ms, TestObject);
                Array = ms.ToArray();
                return Array.Length;
            }
            public Tuple<string, string> PrepareForResponse(string Request, string Content, string action)
            {
                ++ApiRequestCount;
                string FS = null, ContentType = "text/json";
                action = action.Substring(action.IndexOf('/') + 1);
                try
                {
                    if (Request.Contains("GET") || Request.Contains("JSON"))
                    {
                        if (action == "trace")
                        {
                            --ApiRequestCount;

                            string resp = NUWM.Servers.Core.Reactor.Current.GetBaseTrace();
                            var sf = GetObjectSize(UptimePool.Current);
                            resp += "\nSizeOf pool: " + sf + " => " + Tools.ConvertBytesToMegabytes(sf).ToString("F3") + "mb";
                            sf = GetObjectSize(Servers.Core.Reactor.Server.Current);
                            resp += "\nSizeOf server: " + sf + " => " + Tools.ConvertBytesToMegabytes(sf).ToString("F3") + "mb"; ;
                            if (Logger.Log != null && Logger.Log.Count > 0)
                            {
                                var f = Logger.Log.TakeLast(20);
                                resp += "\n\nLOG: (last " + f.Count() + " req)\n";
                                foreach (var h in f.Reverse())
                                    resp += h + "\n";
                                var g = LogScheduler.Current.SavingIn();
                                resp += "\nLog saving in " + g.Hours + "h " + g.Minutes + "m " + g.Seconds + "s\n";

                            }
                            else
                            {
                                resp += "\n\nLog is clear\n";
                            }
                            FS = resp; ContentType = "text/plain";
                        }
                        else if (action == "stat")
                        {
                            FS = GetStats();
                        }
                        else if (action == "set")
                        {
                            --ApiRequestCount;
                            var t = Setting();
                            FS = t.Item1; ContentType = t.Item2;
                        }
                        else
                            throw new FormatException("InvalidRequest: invalid key parameter");

                    }
                    else if (Request.Contains("POST"))
                    {
                        FS = "NOT IMPLEMENTED";
                    }
                }
                catch (NullReferenceException) { throw new OperationCanceledException(); }
                catch (Exception ex)
                {
                    FS = Serialize(ResponseTyper(ex));
                }

                return new Tuple<string, string>(FS, ContentType);
            }

            private string GetStats()
            {
                UptimePool.StatsInfo obj = null;

                if (query.ContainsKey("key"))
                {
                    if (query.ContainsKey("port"))
                    {
                        obj = UptimePool.Current.GetStatus(query["key"], query["port"]);
                    }
                    else
                        obj = UptimePool.Current.GetStatus(query["key"]);
                }

                return JsonConvert.SerializeObject(
                    new Response()
                    {
                        Content = obj,
                        Cache = false,
                        Code = StatusCode.Success,
                        Error = null
                    });
            }

            public Tuple<string, string> Setting()
            {
                string FS = "", ContentType = "text/plain";
                if (query.ContainsKey("ulog"))
                {
                    foreach (var i in Logger.Log)
                    {
                        FS += i; FS += "\n";
                    }
                    if (FS == null)
                        FS = "NOTHING";
                }
                else if (query.ContainsKey("flush_err"))
                {
                    Logger.Errors.Clear();
                    FS = "Cleared errors";
                }
                else if (query.ContainsKey("errors"))
                {
                    if (Logger.Errors.Count > 0)
                        foreach (var i in Logger.Errors)
                            FS += i.Message + "\n" + i.StackTrace + "\n\n";
                    else FS = "All is bright!";
                }
                else if (query.ContainsKey("svlog"))
                {
                    LogScheduler.Current.LogManage();
                    FS = "OK. Log managed";
                }
                else if (query.ContainsKey("clear_stats"))
                {
                    UserStats.Current.ClearAll();
                    FS = "Stats cleared";
                }
                else if (query.ContainsKey("users"))
                {
                    int it = 0;
                    FS += "Users:";
                    foreach (var i in UserStats.UniqueUsersList)
                        FS += "\n[" + ++it + "] " + i;
                    FS += "\nENDOFLIST";
                }
                else if (query.ContainsKey("suspend"))
                {
                    FS = "SUSPENDING";
                    if (this.headers.ContainsKey("user-agent") && this.headers["user-agent"].Contains("MaxRev"))
                        DelaySuspend();
                    else FS = "You are stupid bot";
                }
                else
                {
                    FS = "NOT IMPLEMENTED"; ContentType = "text/plain";
                }
                return new Tuple<string, string>(FS, ContentType);
            }
            private async void DelaySuspend()
            {
                await Task.Delay(2 * 1000);
                Environment.Exit(0);
            }
            private static Response ResponseTyper(Exception err, object obj = null)
            {
                Response resp;
                if (err == null)
                {
                    resp = new Response()
                    {
                        Code = StatusCode.Success,
                        Error = null,
                        Content = obj
                    };
                }
                else
                if (err != null && err.GetType() == typeof(FormatException))
                    resp = new Response() { Code = StatusCode.InvalidRequest, Error = err.Message, Content = null };
                else if (err != null && err.GetType() == typeof(InvalidOperationException))
                {
                    resp = new Response() { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
                }
                else if (err != null && err.GetType() == typeof(EntryPointNotFoundException))
                {
                    resp = new Response() { Code = StatusCode.DeprecatedMethod, Error = err.Message, Content = null };
                }
                else if (err != null && err.GetType() == typeof(InvalidDataException))
                {
                    resp = new Response() { Code = StatusCode.NotFound, Error = err.Message, Content = null };
                }
                else
                {
                    Logger.Errors.Add(err);
                    resp = new Response() { Code = StatusCode.Undefined, Error = (err != null) ? err.Message + "\n" + err.StackTrace : "", Content = obj };
                }

                return resp;
            }

            public static string CreateErrorResp(Exception err)
            {
                Response resp;
                if (err != null)
                {
                    resp = ResponseTyper(err, null);
                }
                else
                {
                    resp = new Response()
                    {
                        Code = StatusCode.Undefined,
                        Error = "NOT IMPLEMENTED",
                        Content = null
                    };
                }
                return JsonConvert.SerializeObject(resp);

            }

            private static string Serialize(object data)
            {
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
                settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
                return JsonConvert.SerializeObject(data, settings);
            }

        }

    }
}
