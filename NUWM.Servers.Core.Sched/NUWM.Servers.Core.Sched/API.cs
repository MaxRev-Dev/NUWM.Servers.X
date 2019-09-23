using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MaxRev.Servers.API;
using MaxRev.Servers.Core.Exceptions;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.Sched
{
    [RouteBase("api")]
    public sealed class API : CoreApi
    {
        protected override void OnInitialized()
        {
            base.OnInitialized();
            if (ModuleContext != default)
            {
                ModuleContext.StreamContext.KeepAlive = false;
                Builder.ContentType("text/json");
            }
        }

        public async Task<IResponseInfo> PrepareForResponse(string Request, string Content, string action)
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
                                FS = await Schedule().ConfigureAwait(false);
                                break;
                            }
                        case "lect":
                            {
                                FS = Lect();
                                break;
                            }
                        case "set":
                            {
                                return await SettingTop();
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

            return Builder.Content(FS).ContentType(ContentType).Build();
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
                    var date = "";
                    if (query.HasKey("content"))
                    {
                        var contents = query["content"];
                        if (query.HasKey("auto"))
                        {
                            var _ = query["auto"];
                        }
                        if (query.HasKey("date"))
                        {

                            date = query["date"];
                        }
                        var res = SubjectParser.Current.Parse(date, WebUtility.UrlDecode(contents), lect);
                        return CreateResponseSubjects(res.Item1, res.Item2);
                    }
                    throw new FormatException("InvalidRequest: expected content parameter");
                }
                throw new FormatException("InvalidRequest: expected lect parameter");
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(ResponseTyper(ex));
            }
        }

        [Route("trace")]
        private string GetTrace()
        {
            Server.State.OnApiResponse();
            Server.State.DecApiResponseUser();
            Builder.ContentType("text/plain");
            return Tools.GetBaseTrace(ModuleContext.Server);
        }


        [Route("sched")]
        public async Task<string> Schedule()
        {
            var query = Info.Query;
            try
            {
                var auto = AutoReplaceHelper.Current?.Now ?? false;
                if (query.HasKey("auto"))
                {
                    var autox = query["auto"];
                    auto = autox == "true" || autox == "1";
                }
                if (query.HasKey("group") || query.HasKey("name"))
                {
                    var group = Uri.UnescapeDataString(query["group"] ?? "");
                    var name = Uri.UnescapeDataString(query["name"] ?? "");

                    var year = TimeChron.GetRealTime().Year;
                    if (query.HasKey("year"))
                    {
                        var ryear = query["year"];
                        year = int.Parse(ryear);
                    }

                    var type = ScheduleKitchen.RetType.days;
                    if (query.HasKey("type"))
                    {
                        var rtype = query["type"];
                        if (rtype == "weeks")
                        {
                            type = ScheduleKitchen.RetType.weeks;
                        }
                        else if (rtype == "days")
                        {
                            type = ScheduleKitchen.RetType.days;
                        }
                        else
                        {
                            throw new InvalidOperationException("InvalidKey: type can be only 'days' or 'weeks'");
                        }
                    }
                    if (query.HasKey("sdate"))
                    {
                        var sdate = query["sdate"];
                        if (query.HasKey("edate"))
                        {
                            var edate = query["edate"];
                            var isLecturer = !string.IsNullOrEmpty(name);

                            var data = new ScheduleKitchen(isLecturer ? name : group, sdate, edate, isLecturer, type);

                            var rp = await data.GetDaysAsync(auto).ConfigureAwait(false);
                            if (data.R != null)
                            {
                                return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser { Data = rp }));
                            }

                            return JsonConvert.SerializeObject(new Response
                            {
                                Code = StatusCode.Success,
                                Content = new ScheduleVisualiser
                                {
                                    Data = rp
                                },
                                Error = null
                            });
                        }
                        return CreateErrorResp(new InvalidOperationException("InvalidKey: edate expected - format dd.MM.yyyy"));
                    }
                    if (query.HasKey("week"))
                    {
                        var rweek = query["week"];
                        var week = int.Parse(rweek);
                        CheckWeekRange(week);

                        var isLecturer = !string.IsNullOrEmpty(name);

                        var cury = TimeChron.GetRealTime().Year;
                        if (year > cury + 1 || year < cury - 1)
                        {
                            throw new InvalidOperationException("InvalidKey: year must be in bounds (current year+-1)");
                        }

                        var data = new ScheduleKitchen(isLecturer ? name : group, week, year, isLecturer, type);
                        var rp = await data.GetDaysAsync(auto).ConfigureAwait(false);
                        if (data.R != null)
                        {
                            return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser { Data = rp }));
                        }

                        return JsonConvert.SerializeObject(new Response
                        {
                            Code = StatusCode.Success,
                            Content = new ScheduleVisualiser
                            {
                                Data = rp
                            },
                            Error = null
                        });
                    }
                    if (query.HasKey("weeks"))
                    {
                        var rweek = query["weeks"];
                        string[] weeks = null;
                        if (rweek.Contains(','))
                        {
                            weeks = rweek.Split(',');
                        }
                        var t1 = int.Parse((weeks ?? throw new BadRequestException("weeks must contain , separator")).First());
                        var t2 = int.Parse(weeks.Last());
                        CheckWeekRange(t1, t2);
                        foreach (var weekq in weeks)
                        {
                            var week = int.Parse(weekq);
                            if (week > 52) week = 52;
                            if (/*week > 52 ||*/ week < 1)
                            {
                                throw new InvalidOperationException("InvalidKey: week is not valid");
                            }
                        }
                        var isLecturer = !string.IsNullOrEmpty(name);

                        var cury = TimeChron.GetRealTime().Year;
                        if (year > cury + 1 || year < cury - 1)
                        {
                            throw new InvalidOperationException("InvalidKey: year must be in bounds (current year+-1)");
                        }

                        var data = new ScheduleKitchen(isLecturer ? name : group, t1, t2, year, isLecturer, type);
                        var rp = await data.GetDaysAsync(auto).ConfigureAwait(false);
                        if (data.R != null)
                        {
                            return JsonConvert.SerializeObject(ResponseTyper(data.R, new ScheduleVisualiser { Data = rp }));
                        }

                        return JsonConvert.SerializeObject(new Response
                        {
                            Code = StatusCode.Success,
                            Content = new ScheduleVisualiser
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
                return JsonConvert.SerializeObject(new Response
                {
                    Code = StatusCode.GatewayTimeout,
                    Cache = false,
                    Content = null,
                    Error = "GatewayTimeout"
                });
            }
            catch (Exception ex)
            {
                Server.Logger.Notify(LogArea.Other, LogType.Info,
                    $"{ex.GetType()}:{ex.Message}\n{WebUtility.UrlDecode(ModuleContext.Request)}");
                return JsonConvert.SerializeObject(ResponseTyper(ex));
            }
        }

        private void CheckWeekRange(params int[] weeks)
        {
            foreach (var week in weeks)
                if (week > 52 || week < 1)
                {
                    throw new InvalidOperationException("InvalidKey: week is not valid");
                }
        }

        [Route("set")]
        public async Task<IResponseInfo> SettingTop()
        {
            var query = Info.Query;

            var core = MainApp.Current.Core;
            string FS, ContentType = "text/plain";
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
                if (core.Logger.GetErrorsString() is var s && !string.IsNullOrEmpty(s))
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
                var p = await SubjectParser.Current.UpdatePatern();
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
            else
            {
                FS = "NOT IMPLEMENTED"; ContentType = "text/plain";
            }
            return Builder.Content(FS).ContentType(ContentType).Build();
        }

        [Route("lect")]
        public string Lect()
        {
            var query = Info.Query;

            try
            {
                if (query.HasKey("name"))
                {
                    var name = query["name"]; string surn;

                    if (name.Contains(' '))
                    {
                        surn = name.Substring(name.IndexOf(' '));
                    }
                    else
                    {
                        surn = name;
                    }

                    var obj =
                        AutoReplaceHelper.Dictionary.Keys.Where(x => x.ToLower().Contains(surn)).ToArray();
                    if (obj.Any())
                    {
                        return JsonConvert.SerializeObject(AutoReplaceHelper.Dictionary[obj.First()]);
                    }

                    throw new InvalidOperationException("Collection not ready");
                }

                if (query.HasKey("subj"))
                {
                    var name = query["subj"];


                    var obj = AutoReplaceHelper.SmartSearch(name);
                    if (obj.Any())
                    {
                        return JsonConvert.SerializeObject(obj);
                    }

                    throw new InvalidOperationException("Collection not ready");
                }
                throw new FormatException("InvalidRequest: name expected");
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(ResponseTyper(ex));
            }
        }

        public static string CreateErrorResp(Exception err)
        {
            Response resp;
            if (err != null)
            {
                resp = ResponseTyper(err);
            }
            else
            {
                resp = new Response
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
                resp = new Response
                {
                    Code = StatusCode.Success,
                    Error = null,
                    Content = obj
                };
            }
            else if (err.GetType() == typeof(FormatException))
            {
                resp = new Response { Code = StatusCode.InvalidRequest, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(DivideByZeroException))
            {
                resp = new Response { Code = StatusCode.ServerNotResponsing, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(InvalidOperationException))
            {
                resp = new Response { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(EntryPointNotFoundException))
            {
                resp = new Response { Code = StatusCode.DeprecatedMethod, Error = err.Message, Content = null };
            }
            else if (err is InvalidDataException)
            {
                resp = new Response { Code = StatusCode.NotFound, Error = err.Message, Content = null };
            }
            else
            {
                MainApp.Current.Core.Logger.NotifyError(LogArea.Other, err);
                resp = new Response { Code = StatusCode.Undefined, Error = err.Message + "\n" + err.StackTrace, Content = obj };
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
                resp = new Response
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
                resp = new Response
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