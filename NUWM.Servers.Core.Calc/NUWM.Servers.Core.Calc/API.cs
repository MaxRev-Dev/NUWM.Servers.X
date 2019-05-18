using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaxRev.Servers.API;
using MaxRev.Servers.Core.Http;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using MaxRev.Utils.Methods;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.Calc
{
    [RouteBase("api")]
    internal class CalcAPI : CoreApi
    {
        protected override void OnInitialized()
        {
            base.OnInitialized();
          //  App.Get.Core.Logger.Notify(LogArea.Other, LogType.Info, Info?.a);
        }
        public Query Query => Info.Query;

        private void NotifyLoggerError(Exception ex)
        {
            App.Get.Core.Logger.NotifyError(LogArea.Other, ex);
        }

        [Route("feedback", AccessMethod.POST)]
        public string FeedbackPost()
        {
            var gu = StatusCode.Success;
            string cont;
            if (FeedbackHandler(Info.FormData))
                cont = "Дякуємо за Ваш відгук!";
            else
            {
                cont = "Ваш відгук не зараховано. Перевищено кількість запитів. Повторіть спробу за декілька хвилин";
                gu = StatusCode.ServerSideError;
            }
            return new Response() { Content = cont, Code = gu }.Serialize();
        }

        private bool FeedbackHandler(IRequestData Content)
        {
            try
            {
                var qur = Content.Form;
                if (!qur.TryGetValue("mail", out var c1) ||
                    !App.Get.FeedbackbHelper.Checker(c1))
                {
                    return false;
                }

                qur.TryGetValue("text", out var c2);
                App.Get.FeedbackbHelper.Feed.Add(c1 + " => " + TimeChron.GetRealTime().ToString("hh:mm:ss - dd.MM.yyyy"), c2);
            }
            catch { return false; }
            return true;
        }
        [Route("trace")]
        private string Tracer()
        {
            this.Server.State.OnApiResponse();
            this.Server.State.DecApiResponseUser();

            var m = App.Get.ReparseScheduler.ScheduledTime - TimeChron.GetRealTime();

            StringBuilder resp = new StringBuilder();
            resp.Append(Tools.GetBaseTrace(Server));

            resp.Append("\n\nSpecialties count: " + App.Get.SpecialtyParser.SpecialtyList.Count +
                "\nSpecialty parser encounter: " +
                $"{m.Days}d {m.Hours}h {m.Minutes}m {m.Seconds}s");

            return resp.ToString();
        }


        [Route("ctable")]
        private string GetTable()
        {
            List<string[]> list = new List<string[]>();
            foreach (var i in App.Get.SpecialtyParser.ConverterTable)
                list.Add(new[] { i.Key.ToString("f1"), i.Value.ToString() });
            list.Reverse();
            return JsonConvert.SerializeObject(new Response() { Content = list, Code = StatusCode.Success, Error = null });
        }

        [Route("set")]
        public async Task<IResponseInfo> SettingTop()
        {
            string FS = "", ContentType = "text/plain";

            if (Query.HasKey("feedback"))
            {
                var f = App.Get.FeedbackbHelper.GetAll(true);
                FS = (string.IsNullOrEmpty(f) ? "NO Reviews(" : f);
            }
            else if (Query.HasKey("svrev"))
            {
                App.Get.FeedbackbHelper.Save();
                FS = "OK. Reviews saved";
            }
            else if (Query.HasKey("list_sp"))
            {
                string resp = "";
                int cnt = 1;
                foreach (var i in App.Get.SpecialtyParser.SpecialtyList)
                {
                    resp += "\n[" + cnt++ + "] " + i.Title + " : " + i.SubTitle + "\n  <-> " + i.URL + "\n";
                }
                FS = resp; ContentType = "text/plain";
            }
            else if (Query.HasKey("load"))
            {
                await Task.Run(async () => { await App.Get.LoadCache(); });
                FS = "Loading cache";
            }
            else if (Query.HasKey("save"))
            {
                await App.Get.SaveCache();
                FS = "Cache saved";
            }
            else
            {
                if (Query.HasKey("reparse"))
                {
                    if (Tools.CheckForInternetConnection())
                    {
                        App.Get.SpecialtyParser.RunAsync();
                        FS = "Started reparse task";
                    }
                    else FS = "Error with DNS";
                }
                //if ((Headers.HeaderExists("user-agent") && Headers.GetHeaderValueOrNull("user-agent").Contains("MaxRev")) ||
                //    Headers.HeaderExists("mx-ses") &&
                //    App.Get.Core.AuthManager.IsLogined(null, Headers.GetHeaderValueOrNull("mx-ses")))
                //{

                //}
                //else FS = "Anautorized";
            }
            if (string.IsNullOrEmpty(FS)) FS = "Context undefined";
            return Builder.Content(FS).ContentType(ContentType).Build();
        }

        #region Specialties

        [Route("specAll")]
        public IResponseInfo All()
        {
            return Builder.Content(new ResponseWraper()
            {
                Code = (string.IsNullOrEmpty(App.Get.SpecialtyParser.HasError)) ? StatusCode.Success : StatusCode.ServerSideError,
                ResponseContent = new SpecialtiesVisualiser() { List = App.Get.SpecialtyParser.SpecialtyList },
            }).Build();
        }
        [Route("calc")]
        public IResponseInfo Calc()
        {
            Exception err;
            List<SpecialtyInfo> obj = new List<SpecialtyInfo>();
            try
            {
                if (Query.Parameters.Count == 1 && Query.HasKey("all"))
                {
                    return All();
                }
                if (Query.HasKey("hlp"))
                {
                    if (string.IsNullOrEmpty(Query["hlp"]))
                    {
                        return CreateStringResponse(string.Join(',', App.Get.SpecialtyParser.GetUnique()), null);
                    }
                    else
                    {
                        string[] keys = Query["hlp"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        List<string> unique = App.Get.SpecialtyParser.GetUnique();
                        foreach (var t in keys)
                        {
                            unique.Remove(t);
                        }
                        return CreateStringResponse(string.Join(',', unique), null);
                    }
                }


                if (Query.HasKey("n"))
                {
                    string[] coefnames = Query["n"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (Query.HasKey("v"))
                    {
                        string[] coefs = Query["v"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (coefnames.Count() != coefs.Count())
                            throw new FormatException("InvalidRequest: non-equal count parameters");

                        int[] vals = new int[coefs.Count()];

                        double avmt = 0;
                        int prior = 0;
                        bool vl = false;
                        for (int i = 0; i < coefs.Count(); i++)
                        {
                            if (!int.TryParse(coefs[i], out int val))
                                throw new FormatException("InvalidRequest: parameter incorrect");
                            vals[i] = val;
                        }
                        if (Query.HasKey("avm"))
                        {
                            if (!double.TryParse(Query["avm"], out double avm))
                            {
                                throw new FormatException("InvalidRequest: parameter incorrect - double expected");
                            }
                            avmt = avm;
                        }
                        if (Query.HasKey("pr"))
                        {
                            if (!int.TryParse(Query["pr"], out int pc))
                            {
                                throw new FormatException("InvalidRequest: parameter incorrect - int expected");
                            }
                            prior = pc;
                        }
                        if (Query.HasKey("vl"))
                        {
                            if (!bool.TryParse(Query["vl"], out bool vill))
                            {
                                throw new FormatException("InvalidRequest: parameter incorrect - bool expected");
                            }
                            vl = vill;
                        }

                        return CreateTpResponse(Calculate(coefnames, vals, avmt, prior, vl), null);

                    }
                    else
                    {
                        throw new FormatException("InvalidRequest: expected v parameter");
                    }
                }
                else
                {
                    throw new FormatException("InvalidRequest: expected n parameter");
                }
            }
            catch (Exception ex)
            {
                err = ex;
            }
            return CreateSPResponse(obj, err);
        }
        private Tuple<List<CalculatedSpecialty>, CalcMarkInfo> Calculate(string[] coefnames, int[] coefs, double averageMark, int prior, bool village)
        {
            var year = 2018;
            var parser = App.Get.SpecialtyParser;
            var obj = new List<CalculatedSpecialty>();
            var listing = App.Get.SpecialtyParser.SpecialtyList.ToArray();
            for (int l = 0; l < coefnames.Count(); l++)
            {
                if (listing.Length > 0)
                {
                    try
                    {
                        listing = listing.Where(x => x.Modulus.CoefName[0] != default && Contains(x.Modulus.CoefName, coefnames)).ToArray();
                    }
                    catch
                    {
                        string all = "";
                        foreach (var i in listing.Where(x => x.Modulus.CoefName.Where(t => t == null).Any()))
                        {
                            all += i.Title + '\n' + i.SubTitle + "\n\n";
                        }
                        NotifyLoggerError(new Exception(all));
                        listing = listing.Where(x => Contains(x.Modulus.CoefName, coefnames, true)).ToArray();
                    }
                }
                else
                {
                    return new Tuple<List<CalculatedSpecialty>, CalcMarkInfo>(obj, default);
                }
            }

            double min = 200, max = 0;
            foreach (var i in listing)
            {
                if (!i.PassMarks.ContainsKey(year)) continue;
                var dictionary = new Dictionary<string, double>();
                var t = i.Modulus.CoefName.ToList();
                var x = i.Modulus.Coef.ToList();

                for (int l = 0; l < t.Count; l++)
                    dictionary.Add(t[l], x[l]);
                double accum = 0;
                string vis = "(";
                for (int el = 0; el < dictionary.Count; el++)
                {
                    string xp = dictionary.Keys.Where(cx => cx.ToLower().Contains(t[el].ToLower())).First();
                    accum += dictionary[xp] * coefs[el];
                    vis += $"{dictionary[xp]} * {coefs[el]} + ";
                }

                var txg = parser.ConverterTable.Keys.Where(v => Math.Abs(Math.Round(v, 1) - averageMark) < 0.00001);
                var enumerable = txg as double[] ?? txg.ToArray();
                if (enumerable.Count() == 0)
                {
                    return new Tuple<List<CalculatedSpecialty>, CalcMarkInfo>(obj, default);
                }

                var tg = enumerable.First();
                accum += 0.1 * parser.ConverterTable[tg];  // aver mark
                vis += $"0.1 * {parser.ConverterTable[tg]}";

                accum *= 1.04; // regional coefs
                vis += $") * 1.04 ";

                //if (parser.SpecialtyList.Where(dx => i.Title.Contains(dx.Title)).Count() == 1) //branch coef
                //{
                if (prior == 1 || prior == 2)
                {
                    accum *= i.BranchCoef;// 1.02;
                    vis += $"* {i.BranchCoef} ";
                }
                // }

                if (village) // village
                {
                    if (i.IsSpecial)//(parser.SpecialtyList.Where(xg => xg.IsSpecial && xg.Code == i.Code).Count() == 1)
                    {
                        accum *= 1.05;
                        vis += $"* 1.05 ";
                    }
                    else
                    {
                        accum *= 1.02;
                        vis += $"* 1.02 ";
                    }
                }

                if (accum > 200) accum = 200;
                if (accum > max) max = accum;
                if (accum < min) min = accum;
                obj.Add(new CalculatedSpecialty(i) { YourAverMark = Math.Round(accum, 1), PassMark = i.PassMarks[year], CalcPath = vis });
            }
            obj.Sort((y, x) => x.YourAverMark.CompareTo(y.YourAverMark)); 
            return new Tuple<List<CalculatedSpecialty>, CalcMarkInfo>(obj,
                new CalcMarkInfo { Aver = (min + max) * 1.0 / 2, Min = min, Max = max });
        }
        #endregion

        public static Response ResponseTyper(Exception err, object obj = null)
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
            if (err.GetType() == typeof(FormatException))
                resp = new Response() { Code = StatusCode.InvalidRequest, Error = err.Message, Content = null };
            else if (err.GetType() == typeof(InvalidOperationException))
            {
                resp = new Response() { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err is InvalidDataException)
            {
                resp = new Response() { Code = StatusCode.NotFound, Error = err.Message, Content = null };
            }

            else
            {
                resp = new Response() { Code = StatusCode.Undefined, Error = err.Message + "\n" + err.StackTrace, Content = obj };
            }

            return resp;
        }

        private bool Contains(string[] arr1, string[] arr2, bool force = false)
        {
            try
            {
                List<int> ch = new List<int>();
                for (int j = 0; j < arr2.Count(); j++)
                {
                    bool found = false;
                    for (int i = 0; i < arr1.Count(); i++)
                    {
                        if (arr1[i].ToLower().Contains(arr2[j].ToLower()) && !ch.Contains(i))
                        {
                            ch.Add(i); found = true; break;
                        }
                    }
                    if (!found) return false;
                }
            }
            catch (Exception ex)
            {
                NotifyLoggerError(new Exception(string.Join(",", arr1) + "\n\n" + string.Join(",", arr2)));
                NotifyLoggerError(ex);
                if (force) return false;
                throw;
            }
            return true;
        }

        public IResponseInfo CreateSPResponse(List<SpecialtyInfo> obj, Exception err)
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
                    Content = new SpecialtiesVisualiser()
                    {
                        List = obj
                    }
                };
            }
            return Builder.Content(JsonConvert.SerializeObject(resp)).Build();
        }
        public IResponseInfo CreateTpResponse(Tuple<List<CalculatedSpecialty>, CalcMarkInfo> obj, Exception err)
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
                    resp = new Response()
                    {
                        Code = StatusCode.NotFound,
                        Error = null,
                        Content = "Not Found"
                    };
                }
                else
                    resp = new Response()
                    {
                        Code = StatusCode.Success,
                        Error = null,
                        Content = new object[] { new SpecialtiesVisualiser() { List = obj.Item1 }, obj.Item2 }
                    };
            }
            return Builder.Content(JsonConvert.SerializeObject(resp)).Build();

        }

        public IResponseInfo CreateStringResponse(string obj, Exception err)
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
            return Builder.Content(JsonConvert.SerializeObject(resp)).Build();
        }
    }
}