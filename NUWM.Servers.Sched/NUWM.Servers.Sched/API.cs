using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace APIUtilty
{
    using HelperUtilties;
    using HierarchyTime;
    using JSON;
    using Server;
    using System.Diagnostics;
    using System.Threading.Tasks;

    class API
    {
        Dictionary<string, string> query;
        public API(Dictionary<string, string> query)
        {
            this.query = query;
        }
        public Dictionary<string, string> Query { get { return query; } set { query = value; } }
        public async Task<Tuple<string, string>> PrepareForResponse(string Request, string Content, string action)
        {
            string FS = null, ContentType = "text/json";
            
            action = action.Substring(action.IndexOf('/') + 1);
            try
            {
                if (Request.Contains("GET") || Request.Contains("JSON"))
                {
                    if (action == "trace")
                    {

                        var t = Process.GetCurrentProcess();
                        var d = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                        var tm = TimeChron.GetRealTime();
                        string resp = @"" + String.Format("Server time: {0}", tm.ToLongTimeString())
                            + String.Format(
                            "\nand NGINX server time: {0} (offset {1} ms)\n\n", DateTime.Now.ToLongTimeString(), TimeChron.Offset.TotalMilliseconds) +
                            "CPU Total: " + t.TotalProcessorTime.Days + "d " + t.TotalProcessorTime.Hours + "h " +
                            +t.TotalProcessorTime.Minutes + "m " + t.TotalProcessorTime.Seconds + "s\n" +
                            "RAM memory size: " + (t.WorkingSet64 / 1048576).ToString() + "mb\n" +
                        String.Format("\nServer uptime: {0}d {1}h {2}m {3}s\n\n", d.Days, d.Hours, d.Minutes, d.Seconds);
                    
                        if (Server.log != null && Server.log.Count > 0)
                        {
                            var f = Server.log.TakeLast(20);
                            resp += "\n\nLOG: (last " + f.Count() + " req)\n";
                            foreach (var h in Server.log)
                                resp += h + "\n";
                        }

                        FS = resp; ContentType = "text/plain";
                    }
                    else if (action == "sched")
                        FS = await Schedule();
                    else if (action == "ulog")
                    {
                        foreach (var i in Server.log)
                        {
                            FS += i; FS += "\n";
                        }
                        if (FS == null) FS = "NOTHING";
                        ContentType = "text/plain";
                    }
                    else if (action == "sched_pattern_update")
                    {
                        var p = Server.CurrentSubjectParser.UpdatePatern();
                        FS = "{code:100; response: " + (p ? "Pattern Updated" :
                             "Pattern not updated or failed to find file") + ", error:null}";
                        ContentType = "text/json";
                    }
                    else if (action == "ar_update")
                    {
                        var p = Server.CurrentSubjectParser.UpdateAutoreplace();
                        FS = "{code:100; response: " + (p ? "Dictionary Updated" :
                             "Dictionary wasn`t updated or failed to find file") + ", error:null}";
                        ContentType = "text/json";
                    }
                    else
                        throw new FormatException("InvalidRequest: invalid key parameter");

                }
                else if (Request.Contains("POST"))
                {
                    if (action == "sched")
                    {
                        if (Request.Contains("POST"))
                        {
                            foreach (var kp in Content.Split('&', StringSplitOptions.RemoveEmptyEntries))
                            {
                                var k_p = kp.Split('=');
                                query.Add(k_p[0], k_p[1]);
                            }
                            bool lect = false;
                            if (query.ContainsKey("lect"))
                            {
                                query.TryGetValue("lect", out string rlect);
                                lect = bool.Parse(rlect);
                                if (query.ContainsKey("content"))
                                {
                                    query.TryGetValue("content", out string contents);
                                    var res = Server.CurrentSubjectParser.Parse(System.Net.WebUtility.UrlDecode(contents), lect);
                                    FS = APIUtilty.API.CreateResponseSubjects(res.Item1, res.Item2);
                                }
                                throw new FormatException("InvalidRequest: expected content parameter");
                            }
                            throw new FormatException("InvalidRequest: expected lect parameter");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FS = Serialize(ResponseTyper(ex));
            }
            return new Tuple<string, string>(FS, ContentType);
        }
        public async Task<string> Schedule()
        {
            try
            {
                if (query.ContainsKey("group") || query.ContainsKey("name"))
                {
                    query.TryGetValue("group", out string group);
                    query.TryGetValue("name", out string name);

                        int year = -100;
                    if (query.ContainsKey("year"))
                    {
                        query.TryGetValue("year", out string ryear);
                        year = int.Parse(ryear);
                    }

                    var type = DataSpace.GetData.RetType.days;
                    if (query.ContainsKey("type"))
                    {
                        query.TryGetValue("type", out string rtype);
                        if (rtype == "weeks") type = DataSpace.GetData.RetType.weeks;
                        else if (rtype == "days") type = DataSpace.GetData.RetType.days;
                        else throw new InvalidOperationException("InvalidKey: type can be only 'days' or 'weeks'");
                    }
                    if (query.ContainsKey("sdate"))
                    {
                        query.TryGetValue("sdate", out string sdate);
                        if (query.ContainsKey("edate"))
                        {
                            query.TryGetValue("edate", out string edate);
                            bool isLecturer = !string.IsNullOrEmpty(name);
                            DataSpace.GetData data = new DataSpace.GetData(isLecturer ? name : group, sdate, edate, isLecturer, type);
                            var rp = await data.GetDays();
                            if (data.R != null)
                                return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser() { Data = rp }));
                            return JsonConvert.SerializeObject(new Response()
                            {
                                Code = StatusCode.Success,
                                Content = new ScheduleVisualiser()
                                {
                                    Data = rp
                                },
                                Error = "null"
                            });
                        }
                        return CreateErrorResp( new InvalidOperationException("InvalidKey: edate expexted - format dd.MM.yyyy"));
                    }
                    else if (query.ContainsKey("week"))
                    {
                        query.TryGetValue("week", out string rweek);
                        int week = int.Parse(rweek);
                        if (week > 52 || week < 1) throw new InvalidOperationException("InvalidKey: week is not valid");
                        bool isLecturer = !string.IsNullOrEmpty(name);

                        int cury = TimeChron.GetRealTime().Year;
                        if (year > cury + 1 || year < cury - 1) throw new InvalidOperationException("InvalidKey: year must be in bounds (current year+-1)");
                        DataSpace.GetData data = new DataSpace.GetData(isLecturer ? name : group, week, year, isLecturer, type);
                        var rp = await data.GetDays();
                        if (data.R != null)
                            return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser() { Data = rp }));
                        return JsonConvert.SerializeObject(new Response()
                        {
                            Code = StatusCode.Success,
                            Content = new ScheduleVisualiser()
                            {
                                Data = rp
                            },
                            Error = "null"
                        });
                    }
                    else if (query.ContainsKey("weeks"))
                    {
                        query.TryGetValue("weeks", out string rweek);
                        string[] weeks = null; 
                        if (rweek.Contains(','))
                        {
                            weeks = rweek.Split(',');
                        }
                        var t1 = int.Parse(weeks.First());
                        var t2 = int.Parse(weeks.Last());

                        foreach (var weekq in weeks)
                        {
                            int week = int.Parse(weekq);
                            if (week > 52 || week < 1) throw new InvalidOperationException("InvalidKey: week is not valid");
                        }
                        bool isLecturer = !string.IsNullOrEmpty(name);

                        int cury = TimeChron.GetRealTime().Year;
                        if (year > cury + 1 || year < cury - 1) throw new InvalidOperationException("InvalidKey: year must be in bounds (current year+-1)");
                        DataSpace.GetData data = new DataSpace.GetData(isLecturer ? name : group, t1,t2, year, isLecturer, type);
                        var rp = await data.GetDays();
                        if (data.R != null)
                            return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser() { Data = rp }));
                        return JsonConvert.SerializeObject(new Response()
                        {
                            Code = StatusCode.Success,
                            Content = new ScheduleVisualiser()
                            {
                                Data = rp
                            },
                            Error = "null"
                        });
                    }
                    return CreateErrorResp(new InvalidOperationException("InvalidKey: sdate expexted - format dd.MM.yyyy"));
                }
                return CreateErrorResp( new InvalidOperationException("InvalidKey: group expexted"));
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(ResponseTyper(ex, null));
            }
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

        private static Response ResponseTyper(Exception err, object obj = null)
        {
            Response resp;
            if (err == null)
            {
                resp = new Response()
                {
                    Code = StatusCode.Success,
                    Error = "null",
                    Content = obj
                };
            }
            else
            if (err != null && err.GetType() == typeof(FormatException))
                resp = new Response() { Code = StatusCode.InvalidRequest, Error = err.Message + "\n" + err.StackTrace, Content = null };
            else if (err != null && err.GetType() == typeof(InvalidOperationException))
            {
                resp = new Response() { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err != null && err.GetType() == typeof(InvalidDataException))
            {
                resp = new Response() { Code = StatusCode.NotFound, Error = err.Message + "\n" + err.StackTrace, Content = null };
            }
            else
            {
                resp = new Response() { Code = StatusCode.Undefined, Error = (err != null) ? err.Message + "\n" + err.StackTrace : "", Content = obj };
            }

            return resp;
        }
        
        public static string CreateResponseSubjects(DayInstance result, Exception err)
        {
            Response resp;
            if (err != null)
            {
                resp = ResponseTyper(err);
            }
            else
            {
                if (err != null)
                {
                    resp = new Response()
                    {
                        Code = StatusCode.ServerSideError,
                        Error = null,
                        Content = "Not Found"
                    };
                }
                else
                    resp = new Response()
                    {
                        Code = StatusCode.Success,
                        Error = null,
                        Content = result
                    };

            }
            return JsonConvert.SerializeObject(resp);
        }

        public string CreateStringResponse(string obj, Exception err)
        {
            Response resp;
            if (err != null)
            {
                resp = ResponseTyper(err);
            }
            else
            {
                resp = new Response()
                {
                    Code = StatusCode.Success,
                    Error = null,
                    Content = obj
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