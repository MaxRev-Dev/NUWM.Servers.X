using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace APIUtilty
{
    using HelperUtilties;
    using JSON;
    using Server;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using static JSON.SpecialtiesVisualiser;
    using static JSON.SpecialtiesVisualiser.Specialty;

    class API
    {
        Dictionary<string, string> query;
        public API(Dictionary<string, string> query)
        {
            this.query = query;
        }
        public Dictionary<string, string> Query { get { return query; } set { query = value; } }
        public Tuple<string, string> PrepareForResponse(string Request, string Content, string action)
        {
            string FS = null, ContentType = "text/json";

            action = action.Substring(action.IndexOf('/') + 1);
            try
            {
                if (Request.Contains("GET") || Request.Contains("JSON"))
                {
                    if (action == "specAll")
                        FS = All();
                    else if (action == "calc")
                        FS = Calc(); 
                    else if (action == "set")
                    {
                        var t = Setting();
                        FS = t.Item1; ContentType = t.Item2;
                    } 
                    else if (action == "trace")
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

                        resp += string.Format("\nUnique users in session: {0}", Server.UserStats.Current.UniqueUsers());
                        resp += string.Format("\nUnique users in last hour: {0}", Server.UserStats.Current.UniqueUsersInHour());

                        TimeSpan m = new TimeSpan();
                        if (ScheduleTask.scheduledTime != null)
                            m = ScheduleTask.scheduledTime - TimeChron.GetRealTime();


                        resp += "\n\nSpecialties count: " + Server.CurrentParser.res.Count +
                            "\nSpecialty parser encounter: " + String.Format("{0}d {1}h {2}m {3}s", m.Days, m.Hours, m.Minutes, m.Seconds);
                        if (Server.log != null && Server.log.Count > 0)
                        {
                            var f = Server.log.TakeLast(20);
                            resp += "\n\nLOG: (last " + f.Count() + " req)\n";
                            foreach (var h in f.Reverse())
                                resp += h + "\n";
                            var g = LogScheduler.scheduledTime - TimeChron.GetRealTime();
                            resp += "\nLog saving in " + g.Hours + "h " + g.Minutes + "m " + g.Seconds + "s\n";
                        }
                        else
                        {
                            resp += "\n\nLog is clear\n";
                        }
                        FS = resp; ContentType = "text/plain";
                    }
                    else
                        throw new FormatException("InvalidRequest: invalid key parameter");

                }
                else if (Request.Contains("POST"))
                { 
                        throw new FormatException("NOT IMPLEMENTED");
                }
            }
            catch (Exception ex)
            {
                FS = Serialize(ResponseTyper(ex));
            }
            return new Tuple<string, string>(FS, ContentType);
        }

        public Tuple<string, string> Setting()
        {
            string FS = "", ContentType = "text/plain";
            if (query.ContainsKey("ulog"))
            {
                foreach (var i in Server.log)
                {
                    FS += i; FS += "\n";
                }
                if (FS == null)
                    FS = "NOTHING";
            }else if (query.ContainsKey("reparse"))
            {
                if (query["reparse"]=="true")
                {
                    if (Server.CheckForInternetConnection()) Server.CurrentParser.GetLastModulus();
                }
                FS = "Started reparse task"; 
            }
            else if (query.ContainsKey("flush_err"))
            {
                Server.Errors.Clear();
                FS = "Cleared errors";
            }
            else if (query.ContainsKey("errors"))
            {
                if (Server.Errors.Count > 0)
                    foreach (var i in Server.Errors)
                        FS += i.Message + "\n" + i.StackTrace + "\n\n";
                else FS = "All is bright!";
            }
            else if (query.ContainsKey("svlog"))
            {
                LogScheduler.LogManage();
                FS = "OK. Log managed";
            }
            else if (query.ContainsKey("list_sp"))
            {
                string resp = "";
                int cnt = 1;
                foreach (var i in Server.CurrentParser.res)
                {
                    resp += "\n[" + cnt++ + "] " + i.Title + " : " + i.SubTitle + "\n  <-> " + i.URL + "\n";
                }
                FS = resp; ContentType = "text/plain";
            }
            return new Tuple<string, string>(FS, ContentType);
        }

        #region Specialties
        public string All()
        {
            return Serialize(new ResponseWraper()
            {
                Code = (String.IsNullOrEmpty(Specialty.SpecialtyParser.Errors)) ? StatusCode.Success : StatusCode.ServerSideError,
                ResponseContent = new SpecialtiesVisualiser() { List = Server.CurrentParser.res },
                Error = Specialty.SpecialtyParser.Errors
            });
        }
        public string Calc()
        {
            Exception err = null;
            List<Specialty> obj = new List<Specialty>();
            try
            {
                if (query.Keys.Count == 1 && query.ContainsKey("all"))
                {
                    return All();
                }
                if (query.ContainsKey("hlp"))
                {
                    if (String.IsNullOrEmpty(query["hlp"]))
                    {
                        return CreateStringResponse(string.Join(',', Server.CurrentParser.GetUnique()), err);
                    }
                    else
                    {
                        string[] keys = query["hlp"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        List<string> unique = Server.CurrentParser.GetUnique();
                        foreach (var t in keys)
                        {
                            unique.Remove(t);
                        }
                        return CreateStringResponse(string.Join(',', unique), err);
                    }
                }


                if (query.ContainsKey("n"))
                {
                    string[] coefnames = query["n"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (query.ContainsKey("v"))
                    {
                        string[] coefs = query["v"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (coefnames.Count() != coefs.Count())
                            throw new FormatException("InvalidRequest: non-equal count parameters");

                        int[] vals = new int[coefs.Count()];

                        double avmt = 0;
                        int pct = 0;
                        bool vl = false;
                        for (int i = 0; i < coefs.Count(); i++)
                        {
                            if (!int.TryParse(coefs[i], out int val))
                                throw new FormatException("InvalidRequest: parameter incorrect");
                            vals[i] = val;
                        }
                        if (query.ContainsKey("avm"))
                        {
                            if (!double.TryParse(query["avm"], out double avm))
                            {
                                throw new FormatException("InvalidRequest: parameter incorrect - double expected");
                            }
                            avmt = avm;
                        }
                        if (query.ContainsKey("pr"))
                        {
                            if (!int.TryParse(query["pr"], out int pc))
                            {
                                throw new FormatException("InvalidRequest: parameter incorrect - int expected");
                            }
                            pct = pc;
                        }
                        if (query.ContainsKey("vl"))
                        {
                            if (!bool.TryParse(query["vl"], out bool vill))
                            {
                                throw new FormatException("InvalidRequest: parameter incorrect - bool expected");
                            }
                            vl = vill;
                        }

                        return CreateTpResponse(Calculate(coefnames, vals, avmt, pct, vl), err);

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
        private Tuple<List<Specialty>, CalcMarkInfo> Calculate(string[] coefnames, int[] coefs, double avm, int pct, bool vl)
        {
            var cfn = coefnames.ToList();
            var cf = coefs.ToList();
            List<Specialty> obj = new List<Specialty>();
            double[] res = new double[0];
            var cmi = new CalcMarkInfo();

            var listing = Server.CurrentParser.res.ToArray();
            for (int l = 0; l < coefnames.Count(); l++)
            {
                if (listing.Count() > 0)
                {
                    try
                    {
                        listing = listing.Where(x => Contains(x.Modulus.CoefName, coefnames)).ToArray();
                    }
                    catch
                    {
                        string all="";
                        foreach(var i in listing.Where(x => x.Modulus.CoefName.Where(t=>t==null).Count()>0))
                        {
                            all += i.Title + '\n' + i.SubTitle + "\n\n";
                        }
                        Server.Errors.Add(new Exception(all));
                        listing = listing.Where(x => Contains(x.Modulus.CoefName, coefnames,true)).ToArray();
                    }


                }
                else return new Tuple<List<Specialty>, CalcMarkInfo>(obj, cmi);
            }

            double min = 200, max = 0;
            foreach (var i in listing)
            {
                Dictionary<string, double> dictionary = new Dictionary<string, double>();
                List<string> t = i.Modulus.CoefName.ToList();
                List<double> x = i.Modulus.Coef.ToList();

                for (int l = 0; l < t.Count; l++)
                    dictionary.Add(t[l], x[l]);
                double resx = 0;
                for (int el = 0; el < dictionary.Count; el++)
                {
                    string xp = dictionary.Keys.Where(cx => cx.ToLower().Contains(t[el].ToLower())).First();
                    resx += dictionary[xp] * coefs[el];
                }

                var txg = Specialty.converts.Keys.Where(xd => Math.Round(xd, 1) == avm);
                if (txg.Count() == 0) return new Tuple<List<Specialty>, CalcMarkInfo>(obj, new CalcMarkInfo());
                var tg = txg.First();
                resx += 0.1 * Specialty.converts[tg];  // aver mark
                resx *= 1.02; // regional coefs
                if (Specialty.specList.Where(dx => i.Title.Contains(dx.Name)).Count() == 1) //branch coef
                    if (pct == 1 || pct == 2)
                        resx *= 1.02;
                if (vl) // village
                    if (Specialty.specList.Where(xg => xg.Special && xg.InnerCode == i.Code).Count() == 1)
                        resx *= 1.05;
                    else
                        resx *= 1.02;
                if (resx > 200) resx = 200;
                if (resx > max) max = resx;
                if (resx < min) min = resx;
                res.Append(resx);
                obj.Add(i);
                obj.Last().YourAverMark = Math.Round(resx, 1).ToString();
            }
            obj.Sort((y, x) => double.Parse(x.YourAverMark).CompareTo(double.Parse(y.YourAverMark)));
            obj.OrderBy(x => double.Parse(x.YourAverMark));
            return new Tuple<List<Specialty>, Specialty.CalcMarkInfo>(obj, new Specialty.CalcMarkInfo() { Aver = (min + max) / 2, Min = min, Max = max });
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
            if (err != null && err.GetType() == typeof(FormatException))
                resp = new Response() { Code = StatusCode.InvalidRequest, Error = err.Message, Content = null };
            else if (err != null && err.GetType() == typeof(InvalidOperationException))
            {
                resp = new Response() { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err != null && err.GetType() == typeof(InvalidDataException))
            {
                resp = new Response() { Code = StatusCode.NotFound, Error = err.Message, Content = null };
            }

            else
            {
                resp = new Response() { Code = StatusCode.Undefined, Error = (err != null) ? err.Message + "\n" + err.StackTrace : "", Content = obj };
            }

            return resp;
        }

        private bool Contains(string[] arr1, string[] arr2,bool force= false)
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
            catch (Exception ex) { Server.Errors.Add(new Exception(string.Join(",", arr1)+"\n\n"+ string.Join(",", arr2))); Server.Errors.Add(ex);
                if (force) return false;
                throw ex;
            }
            return true;
        }

        public static string CreateSPResponse(List<Specialty> obj, Exception err)
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
            return JsonConvert.SerializeObject(resp);

        }
        public static string CreateTpResponse(Tuple<List<Specialty>, Specialty.CalcMarkInfo> obj, Exception err)
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
                        Content = new object[]{ new SpecialtiesVisualiser()
                    {
                        List = obj.Item1
                    }, obj.Item2}
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