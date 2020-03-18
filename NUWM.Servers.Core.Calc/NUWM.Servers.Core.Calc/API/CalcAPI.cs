using MaxRev.Servers.API;
using MaxRev.Servers.Core.Http;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaxRev.Servers.API.Controllers;
using MaxRev.Servers.Utils.Logging;
using NUWM.Servers.Core.Calc.Models;
using NUWM.Servers.Core.Calc.Services;
using NUWM.Servers.Core.Calc.Services.Parsers;

namespace NUWM.Servers.Core.Calc.API
{
    [RouteBase("api")]
    internal class CalcAPI : CoreApi
    {
        private Query Query => Info.Query;

        private void NotifyLoggerError(Exception ex)
        {
            Server.Logger.NotifyError(LogArea.Other, ex);
        }

        #region Service

        [Route("trace")]
        private string Tracer()
        {  
            Server.Features.GetFeature<State>()?.OnApiResponse();
            Server.Features.GetFeature<State>()?.DecApiResponseUser();

            var scheduler = Services.GetRequiredService<ParserScheduler>();
            var m = scheduler.ScheduledTime - TimeChron.GetRealTime();

            var resp = new StringBuilder();
            resp.Append(Tools.GetBaseTrace(ModuleContext));
            var parser = Services.GetRequiredService<SpecialtyParser>();
            resp.Append("\n\nSpecialties count: ");
            resp.Append(parser.SpecialtyList.Count);
            resp.Append("\nSpecialty parser encounter:");
            resp.Append($"{m.Days}d {m.Hours}h {m.Minutes}m {m.Seconds}s");
            return resp.ToString();
        }
        
        [Route("ctable")]
        private string GetTable()
        {
            var parser = Services.GetRequiredService<SpecialtyParser>();
            List<string[]> list = new List<string[]>();
            foreach (var i in parser.ConverterTable)
                list.Add(new[] { i.Key.ToString("f1"), i.Value.ToString() });
            list.Reverse();
            return JsonConvert.SerializeObject(new Response { Content = list, Code = StatusCode.Success, Error = null });
        }

        [Route("set")]
        public async Task<IResponseInfo> SettingTop()
        {
            string FS = "", ContentType = "text/plain";

            var parser = Services.GetRequiredService<SpecialtyParser>();
            var feedbackHelper = Services.GetRequiredService<FeedbackHelper>();
            var cacheHelper = Services.GetRequiredService<CacheHelper>();
            if (Query.HasKey("feedback"))
            {
                var f = feedbackHelper.GetAll(true);
                FS = (string.IsNullOrEmpty(f) ? "NO Reviews(" : f);
            }
            else if (Query.HasKey("svrev"))
            {
                feedbackHelper.Save();
                FS = "OK. Reviews saved";
            }
            else if (Query.HasKey("list_sp"))
            {
                string resp = "";
                int cnt = 1;
                foreach (var i in parser.SpecialtyList)
                {
                    resp += "\n[" + cnt++ + "] " + i.Title + " : " + i.SubTitle + "\n  <-> " + i.URL + "\n";
                }
                FS = resp; ContentType = "text/plain";
            }
            else if (Query.HasKey("load"))
            {
                await Task.Run(async () => { await cacheHelper.LoadCache(); });
                FS = "Loading cache";
            }
            else if (Query.HasKey("save"))
            {
                await cacheHelper.SaveCache();
                FS = "Cache saved";
            }
            else
            {
                if (Query.HasKey("reparse"))
                {
                    if (Tools.CheckForInternetConnection())
                    {
                        parser.RunAsync();
                        FS = "Started reparse task";
                    }
                    else FS = "Error with DNS";
                }
            }
            if (string.IsNullOrEmpty(FS)) FS = "Context undefined";
            return Builder.Content(FS).ContentType(ContentType).Build();
        }

        #endregion

        #region Specialties

        [Route("specAll")]
        public IResponseInfo All()
        {
            var parser = Services.GetRequiredService<SpecialtyParser>();
            return Builder.Content(new ResponseWraper
            {
                Code = (string.IsNullOrEmpty(parser.HasError)) ? StatusCode.Success : StatusCode.ServerSideError,
                ResponseContent = new SpecialtiesVisualiser { List = parser.SpecialtyList }
            }).Build();
        }

        [Route("calc")]
        public IResponseInfo Calc()
        {
            var parser = Services.GetRequiredService<SpecialtyParser>();
            Exception err;
            try
            {
                if (Query.Parameters.Count == 1 && Query.HasKey("all"))
                {
                    return All();
                }

                if (Query.HasKey("hlp"))
                {
                    var unique = parser.GetUniqueSubjectNames().ToList();
                    if (!string.IsNullOrEmpty(Query["hlp"]))
                    {

                        var keys = Query["hlp"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var t in keys)
                        {
                            unique.Remove(t);
                        }
                    }
                    return CreateStringResponse(string.Join(',', unique), null);
                }


                if (Query.HasKey("n"))
                {
                    string[] coefnames = Query["n"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (Query.HasKey("v"))
                    {
                        string[] coefs = Query["v"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (coefnames.Length != coefs.Length)
                            throw new FormatException("InvalidRequest: non-equal count parameters");

                        int[] vals = new int[coefs.Length];

                        double avmt = 0;
                        double? pc = default;
                        int prior = 0;
                        bool vl = false;
                        int? ukrOlimp = default;
                        for (int i = 0; i < coefs.Length; i++)
                        {
                            if (!int.TryParse(coefs[i], out int val))
                                throw new FormatException("InvalidRequest: incorrect parameter. Can't parse coefficients");
                            vals[i] = val;
                        }

                        if (Query.HasKey("avm"))
                        {
                            if (!double.TryParse(Query["avm"], out double avm))
                            {
                                throw new FormatException("InvalidRequest: incorrect parameter - double expected");
                            }
                            avmt = avm;
                        }

                        if (Query.HasKey("pr"))
                        {
                            if (!int.TryParse(Query["pr"], out int pr))
                            {
                                throw new FormatException("InvalidRequest: incorrect parameter - int expected");
                            }
                            prior = pr;
                        }

                        if (Query.HasKey("vl"))
                        {
                            if (!bool.TryParse(Query["vl"], out bool vill))
                            {
                                throw new FormatException("InvalidRequest: incorrect parameter - bool expected");
                            }
                            vl = vill;
                        }

                        if (Query.HasKey("pc"))
                        {
                            if (!double.TryParse(Query["pc"], out double pcm))
                            {
                                throw new FormatException("InvalidRequest: incorrect parameter - double expected");
                            }
                            pc = pcm;
                        }

                        if (Query.HasKey("uo"))
                        {
                            if (!int.TryParse(Query["uo"], out int ukrOlimpr))
                            {
                                throw new FormatException("InvalidRequest: incorrect parameter - int expected");
                            }
                            if (ukrOlimpr < 0 || ukrOlimpr > 2)
                                throw new ArgumentOutOfRangeException($"Index must be 0, 1 or 2");
                            ukrOlimp = ukrOlimpr;
                        }

                        var calc = Services.GetRequiredService<Calculator>().OnError(NotifyLoggerError);
                        return CreateTpResponse(calc.Calculate(parser, coefnames, vals, avmt, prior, vl, pc, ukrOlimp), null);

                    }

                    throw new FormatException("InvalidRequest: expected v parameter");
                }

                throw new FormatException("InvalidRequest: expected n parameter");
            }
            catch (Exception ex)
            {
                err = ex;
            }

            return CreateStringResponse(default, err);
        }

        #endregion

        #region Response convertion (old)

        private static Response ResponseTyper(Exception err, object obj = null)
        {
            Response resp;
            if (err == null)
            {
                resp = new Response
                {
                    Code = StatusCode.Success,
                    Error = "null",

                    Content = obj
                };
            }
            else if (err.GetType() == typeof(FormatException))
                resp = new Response { Code = StatusCode.InvalidRequest, Error = err.Message, Content = null };
            else if (err.GetType() == typeof(InvalidOperationException))
            {
                resp = new Response { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err is InvalidDataException)
            {
                resp = new Response { Code = StatusCode.NotFound, Error = err.Message, Content = null };
            }

            else
            {
                resp = new Response { Code = StatusCode.Undefined, Error = err.Message + "\n" + err.StackTrace, Content = obj };
            }

            return resp;
        }

        private IResponseInfo CreateTpResponse(Tuple<List<CalculatedSpecialty>, CalcMarkInfo> obj, Exception err)
        {
            Response resp;
            if (err != null)
            {
                resp = ResponseTyper(err);
            }
            else
            {
                if (obj.Item1.Count == 0)
                {
                    resp = new Response
                    {
                        Code = StatusCode.NotFound,
                        Error = null,
                        Content = "Not Found"
                    };
                }
                else
                    resp = new Response
                    {
                        Code = StatusCode.Success,
                        Error = null,
                        Content = new object[] { new SpecialtiesVisualiser { List = obj.Item1 }, obj.Item2 }
                    };
            }
            return Builder.Content(JsonConvert.SerializeObject(resp)).Build();

        }

        private IResponseInfo CreateStringResponse(string obj, Exception err)
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
            return Builder.Content(JsonConvert.SerializeObject(resp)).Build();
        }

        #endregion
    }
}