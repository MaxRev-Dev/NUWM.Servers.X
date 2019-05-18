using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MaxRev.Servers.API;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.Sched
{
    [RouteBase("api")]
    public sealed class API : CoreApi
    {
        public async Task<Tuple<string, string>> PrepareForResponse(string Request, string Content, string action)
        {
            string FS = null, ContentType = "text/json";
            action = action.Substring(action.IndexOf('/') + 1);
            try
            {
                if (Request.Contains("GET") || Request.Contains("JSON"))
                {
                    switch (action)
                    {
                        case "trace":
                            {
                                FS = GetTrace(); ContentType = "text/plain";
                                break;
                            }
                        case "sched":
                            {
                                FS = await Schedule();
                                break;
                            }
                        case "lect":
                            {
                                FS = Lect();
                                break;
                            }
                        case "set":
                            {
                                var t = SettingTop();
                                FS = t.Item1; ContentType = t.Item2;
                                break;
                            }
                        default:
                            throw new FormatException("InvalidRequest: invalid key parameter");
                    }
                }
                else if (Request.Contains("POST"))
                {
                    if (action == "sched")
                    {
                    }
                }
            }
            catch (NullReferenceException) { throw new OperationCanceledException(); }
            catch (Exception ex)
            {
                FS = Serialize(ResponseTyper(ex));
            }

            return new Tuple<string, string>(FS, ContentType);
        }
        [Route("schedPost", AccessMethod.POST)]
        public string LectPost()
        {
            var query = Info.Query;

            try
            {
                bool lect;
                if (query.HasKey("lect"))
                {
                    var rlect = query["lect"];
                    lect = bool.Parse(rlect);
                    string date = "";
                    if (query.HasKey("content"))
                    {
                        string contents = query["content"];
                        bool auto = false;
                        if (query.HasKey("auto"))
                        {
                            string autox = query["auto"]; ;
                            auto = (autox == "true" || autox == "1") ? true : false;
                        }
                        if (query.HasKey("date"))
                        {

                            date = query["date"];
                        }
                        var res = SubjectParser.Current.Parse(date, System.Net.WebUtility.UrlDecode(contents), lect, auto, "");
                        return API.CreateResponseSubjects(res.Item1, res.Item2);
                    }
                    throw new FormatException("InvalidRequest: expected content parameter");
                }
                throw new FormatException("InvalidRequest: expected lect parameter");
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(ResponseTyper(ex, null));
            }
        }

        [Route("trace")]
        private string GetTrace()
        {
            this.Server.State.OnApiResponse();
            this.Server.State.DecApiResponseUser();

            return Tools.GetBaseTrace(ModuleContext.Server);
        }


        [Route("sched")]
        public async Task<string> Schedule()
        {
            var query = Info.Query;
            try
            {
                bool auto = AutoReplaceHelper.Current?.Now ?? false;
                if (query.HasKey("auto"))
                {
                    string autox = query["auto"];
                    auto = autox == "true" || autox == "1";
                }
                if (query.HasKey("group") || query.HasKey("name"))
                {
                    string group = Uri.UnescapeDataString(query["group"] ?? "");
                    string name = Uri.UnescapeDataString(query["name"] ?? "");

                    int year = TimeChron.GetRealTime().Year;
                    if (query.HasKey("year"))
                    {
                        string ryear = query["year"];
                        year = int.Parse(ryear);
                    }

                    var type = GetData.RetType.days;
                    if (query.HasKey("type"))
                    {
                        string rtype = query["type"];
                        if (rtype == "weeks")
                        {
                            type = GetData.RetType.weeks;
                        }
                        else if (rtype == "days")
                        {
                            type = GetData.RetType.days;
                        }
                        else
                        {
                            throw new InvalidOperationException("InvalidKey: type can be only 'days' or 'weeks'");
                        }
                    }
                    if (query.HasKey("sdate"))
                    {
                        string sdate = query["sdate"];
                        if (query.HasKey("edate"))
                        {
                            string edate = query["edate"];
                            bool isLecturer = !string.IsNullOrEmpty(name);
                            GetData data = new GetData(isLecturer ? name : group, sdate, edate, isLecturer, type);

                            var rp = await data.GetDays(auto);
                            if (data.R != null)
                            {
                                return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser() { Data = rp }));
                            }

                            return JsonConvert.SerializeObject(new Response()
                            {
                                Code = StatusCode.Success,
                                Content = new ScheduleVisualiser()
                                {
                                    Data = rp
                                },
                                Error = null
                            });
                        }
                        return CreateErrorResp(new InvalidOperationException("InvalidKey: edate expected - format dd.MM.yyyy"));
                    }
                    else if (query.HasKey("week"))
                    {
                        string rweek = query["week"];
                        int week = int.Parse(rweek);
                        if (week > 52 || week < 1)
                        {
                            throw new InvalidOperationException("InvalidKey: week is not valid");
                        }

                        bool isLecturer = !string.IsNullOrEmpty(name);

                        int cury = TimeChron.GetRealTime().Year;
                        if (year > cury + 1 || year < cury - 1)
                        {
                            throw new InvalidOperationException("InvalidKey: year must be in bounds (current year+-1)");
                        }

                        GetData data = new GetData(isLecturer ? name : group, week, year, isLecturer, type);
                        var rp = await data.GetDays(auto);
                        if (data.R != null)
                        {
                            return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser() { Data = rp }));
                        }

                        return JsonConvert.SerializeObject(new Response()
                        {
                            Code = StatusCode.Success,
                            Content = new ScheduleVisualiser()
                            {
                                Data = rp
                            },
                            Error = null
                        });
                    }
                    else if (query.HasKey("weeks"))
                    {
                        string rweek = query["weeks"];
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
                            if (week > 52) week = 52;
                            if (/*week > 52 ||*/ week < 1)
                            {
                                throw new InvalidOperationException("InvalidKey: week is not valid");
                            }
                        }
                        bool isLecturer = !string.IsNullOrEmpty(name);

                        int cury = TimeChron.GetRealTime().Year;
                        if (year > cury + 1 || year < cury - 1)
                        {
                            throw new InvalidOperationException("InvalidKey: year must be in bounds (current year+-1)");
                        }

                        GetData data = new GetData(isLecturer ? name : group, t1, t2, year, isLecturer, type);
                        var rp = await data.GetDays(auto);
                        if (data.R != null)
                        {
                            return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser() { Data = rp }));
                        }

                        return JsonConvert.SerializeObject(new Response()
                        {
                            Code = StatusCode.Success,
                            Content = new ScheduleVisualiser()
                            {
                                Data = rp
                            },
                            Error = null
                        });
                    }
                    return CreateErrorResp(new InvalidOperationException("InvalidKey: sdate expected - format dd.MM.yyyy"));
                }
                return CreateErrorResp(new InvalidOperationException("InvalidKey: group expected"));
            }
            catch (OperationCanceledException)
            {
                return JsonConvert.SerializeObject(new Response()
                {
                    Code = StatusCode.GatewayTimeout,
                    Cache = false,
                    Content = null,
                    Error = "GatewayTimeout"
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(ResponseTyper(ex, null));
            }
        }
        [Route("set")]
        public Tuple<string, string> SettingTop()
        {
            var query = Info.Query;

            var core = MainApp.Current.Core;
            string FS = "Context undefined", ContentType = "text/plain";
            if (query.HasKey("reinit_users")) { core.AuthManager.InitializeUsersDb(); FS = "OK"; }
            else if (query.HasKey("ulog"))
            {
                FS = core.Logger.GetLog();
                if (string.IsNullOrEmpty(FS))
                {
                    FS = "NOTHING";
                }
            }
            else if (query.HasKey("flush_err"))
            {
                core.Logger.ClearErrors();
                FS = "Cleared errors";
            }
            else if (query.HasKey("errors"))
            {
                if (core.Logger.GetErrorsString() is string s && !string.IsNullOrEmpty(s))
                {
                    FS = s;
                }
                else
                {
                    FS = "All is bright!";
                }
            }
            else if (query.HasKey("svlog"))
            {
                Server.Logger.Scheduler.LogManage();
                FS = "OK. Log managed";
            }
            else if (query.HasKey("sched_pattern_update"))
            {
                var p = SubjectParser.Current.UpdatePatern();
                FS = (p ? "Pattern Updated" :
                     "Pattern not updated or failed to find file");
            }
            else if (query.HasKey("ar_update"))
            {
                var p = SubjectParser.Current.UpdateAutoreplace();
                FS = (p ? "Dictionary Updated" :
                     "Dictionary wasn't updated or failed to find file");
            }
            else if (query.HasKey("ar"))
            {
                if (query["ar"] == "true")
                {
                    AutoReplaceHelper.Current.Now = true;
                    FS = "AutoReplace is ON";
                }
                else
                {
                    AutoReplaceHelper.Current.Now = false;
                    FS = "AutoReplace is OFF";
                }
            }
            //else if (query.HasKey("clear_stats"))
            //{
            //    Server.State.ClearAll();
            //    FS = "Stats cleared";
            //}
            //else if (query.HasKey("users"))
            //{
            //    int it = 0;
            //    FS += "Users:";
            //    foreach (var i in UserStats.UniqueUsersList)
            //    {
            //        FS += "\n[" + ++it + "] " + i;
            //    }

            //    FS += "\nENDOFLIST";
            //}
            else
            {
                FS = "NOT IMPLEMENTED"; ContentType = "text/plain";
            }
            return new Tuple<string, string>(FS, ContentType);
        }

        [Route("lect")]
        public string Lect()
        {
            var query = Info.Query;
            var headers = Info.Headers;

            try
            {
                if (query.HasKey("name"))
                {
                    var name = query["name"]; var surn = "";

                    if (name.Contains(' '))
                    {
                        surn = name.Substring(name.IndexOf(' '));
                    }
                    else
                    {
                        surn = name;
                    }

                    var obj =
                        AutoReplaceHelper.Dictionary.Keys.Where(x => x.ToLower().Contains(surn));
                    if (obj.Any())
                    {
                        return JsonConvert.SerializeObject(AutoReplaceHelper.Dictionary[obj.First()]);
                    }
                    else
                    {
                        throw new InvalidOperationException("Collection not ready");
                    }
                }
                else if (query.HasKey("subj"))
                {
                    var name = query["subj"]; var surn = "";

                    if (name.Contains(' '))
                    {
                        surn = name.Substring(name.IndexOf(' '));
                    }
                    else
                    {
                        surn = name;
                    }

                    List<string> obj = new List<string>();
                    obj = AutoReplaceHelper.SmartSearch(name);
                    if (obj.Any())
                    {
                        return JsonConvert.SerializeObject(obj);
                    }
                    else
                    {
                        throw new InvalidOperationException("Collection not ready");
                    }
                }
                throw new FormatException("InvalidRequest: name expected");
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
                    Error = null,
                    Content = obj
                };
            }
            else if (err.GetType() == typeof(FormatException))
            {
                resp = new Response() { Code = StatusCode.InvalidRequest, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(DivideByZeroException))
            {
                resp = new Response() { Code = StatusCode.ServerNotResponsing, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(InvalidOperationException))
            {
                resp = new Response() { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(EntryPointNotFoundException))
            {
                resp = new Response() { Code = StatusCode.DeprecatedMethod, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(InvalidDataException))
            {
                resp = new Response() { Code = StatusCode.NotFound, Error = err.Message, Content = null };
            }
            else
            {
                MainApp.Current.Core.Logger.NotifyError(LogArea.Other, err);
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
                {
                    resp = new Response()
                    {
                        Code = StatusCode.Success,
                        Error = null,
                        Content = result
                    };
                }
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