using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace APIUtilty
{
    using HelperUtilties;
    using JSON;
    using Lead;
    using Server;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using static Lead.ParserPool.Parser;

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

            var pool = Server.CurrentParserPool.POOL;
            action = action.Substring(action.IndexOf('/') + 1);
            try
            {
                if (Request.Contains("GET") || Request.Contains("JSON"))
                {
                    if (pool.ContainsKey(action))
                        FS = await UniversalAsync(pool[action]);

                    else if (action == "getById")
                    {
                        if (Query.ContainsKey("id"))
                        {
                            if (int.TryParse(Query["id"], out int id))
                            {

                                var task = await GetById(id);
                                FS = task ?? throw new FormatException("ID not found in pool");
                                ContentType = "text/json";
                            }
                            throw new FormatException("InvalidRequest: expected institute id");
                        }
                        throw new FormatException("InvalidRequest: expected id");
                    }

                    else if (action == "keys")
                    {
                        FS = "API KEYS:\n sched\nspecAll\ntrace\n" + String.Join('\n', Server.CurrentParserPool.POOL.Keys.ToArray());
                        ContentType = "text/flat";
                    }
                    else if (action == "searchNews")
                    {
                        var task = await SearchNews();
                        FS = task ?? throw new InvalidDataException("Not Found");
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
                            "API KEYS: " + String.Join(", ", Server.CurrentParserPool.POOL.Keys.ToArray()) +
                        String.Format("\nServer uptime: {0}d {1}h {2}m {3}s\n\n", d.Days, d.Hours, d.Minutes, d.Seconds);
                        int uu = 0;
                        if (InstantCache != null)
                        {
                            resp += "\nInstantCache count: " + (uu = ParserPool.Parser.InstantCache.Count);
                        }
                        int countAllnews = uu;
                        foreach (var ert in Server.CurrentParserPool.POOL.Values)
                        {
                            TimeSpan k = new TimeSpan();
                            if (ert.scheduler != null)
                                k = ert.scheduler.scheduledTime - TimeChron.GetRealTime();

                            resp += "\n" + new string('-', 20);
                            if (ert.newslist != null && ert.newslist.Count > 0)
                            {
                                resp += "\nParser: " + ert.xkey +
                                 "\nNews Articles: " + (ert.newslist != null ? ert.newslist.Count : 0) +
                                 "\nNext parsing in: " + String.Format("{0}d {1}h {2}m {3}s", k.Days, k.Hours, k.Minutes, k.Seconds) +
                                "\nCache epoch: " + (ert.scheduler != null ? ert.CacheEpoch : 0);
                            }
                            else
                            {
                                resp += "\nParser " + ert.xkey + " is updating cache";
                            }
                            resp += "\n" + new string('-', 20) + "\n";
                            countAllnews += (ert.newslist != null ? ert.newslist.Count : 0);
                        }

                        resp += "\nAll articles count: " + countAllnews;

                        if (Server.log != null && Server.log.Count > 0)
                        {
                            var f = Server.log.TakeLast(20);
                            resp += "\n\nLOG: (last " + f.Count() + " req)\n";
                            foreach (var h in Server.log)
                                resp += h + "\n";
                        }

                        FS = resp; ContentType = "text/plain";
                    }

                    else if (action == "saveinstcache")
                    {
                        if (query.ContainsKey("key"))
                        {
                            if (query["key"] == "all")
                            {
                                ParserPool.Parser.SaveCache();
                                FS = "saved ALL"; ContentType = "text/plain";


                            }
                            else if (Server.CurrentParserPool.POOL.ContainsKey(query["key"]))
                            {
                                ParserPool.Parser.SaveCache(query["key"]);
                                FS = "saved " + query["key"]; ContentType = "text/plain";

                            }
                            throw new FormatException("InvalidRequest: invalid key parameter");
                        }
                        else
                        {
                            ParserPool.Parser.ScheduleInstantCacheSave.SaveInstantCache();
                            FS = "saved"; ContentType = "text/plain";
                        }
                    }
                    else if (action == "ulog")
                    {
                        foreach (var i in Server.log)
                        {
                            FS += i; FS += "\n";
                        }
                        if (FS == null) FS = "NOTHING";
                        ContentType = "text/plain";
                    }
                    else
                        throw new FormatException("InvalidRequest: invalid key parameter");

                }
                else if (Request.Contains("POST"))
                {
                    throw new FormatException("InvalidRequest: invalid key parameter");
                }
            }
            catch (Exception ex)
            {
                FS = Serialize(ResponseTyper(ex));
            }
            return new Tuple<string, string>(FS, ContentType);
        }

        public async Task<string> UniversalAsync(Lead.ParserPool.Parser parser)
        {
            var newslist = DeepCopy(parser.newslist);
            Exception err = null;
            bool toHTML = false;
            int last = -1;
            List<NewsItem> obj = new List<NewsItem>();
            try
            {
                if (query.ContainsKey("p_id"))
                {
                    query.TryGetValue("p_id", out string param);
                    int iparam = Convert.ToInt32(param);
                    if (newslist.Count() > 0)
                    {
                        if (iparam > newslist.Last().PageId) throw new FormatException("Page ID is out of range");
                        obj = newslist.Where(x => x.PageId == iparam).ToList();

                    }
                    else throw new FormatException("Page ID is out of range");
                }
                if (query.ContainsKey("html"))
                {
                    query.TryGetValue("html", out string param);
                    if (!int.TryParse(param, out int iparam))
                        throw new FormatException("InvalidRequest: expected 1/0 - got " + param);
                    toHTML = (iparam == 1 ? true : false);
                }
                if (query.ContainsKey("uri"))
                {
                    if (obj.Count() > 0)
                        throw new FormatException("InvalidRequest  >> uri & id");
                    query.TryGetValue("uri", out string param);

                    var c = newslist.Where(x => x.Url == param);
                    if (c.Count() > 0) obj.Add(c.First());
                    else throw new InvalidDataException("Not found");
                }
                if (query.ContainsKey("query"))
                {
                    if (obj.Count() > 0)
                        throw new FormatException("InvalidRequest  >> query must be unique in request");
                    query.TryGetValue("query", out string param);

                    foreach (var t in newslist)
                    {
                        try
                        {
                            if (t.Detailed.ContentHTML.Contains(param)) obj.Add(t);
                        }
                        catch (Exception) { }
                    }
                    if (obj.Count == 0) throw new InvalidDataException("Not found");
                }
                if (query.ContainsKey("uriquery"))
                {
                    if (obj.Count() > 0)
                        throw new FormatException("InvalidRequest  >> uriquery must be unique in request");
                    query.TryGetValue("uriquery", out string param);

                    foreach (var t in newslist)
                    {
                        try
                        {
                            if (t.Url.Contains(param)) obj.Add(t);
                        }
                        catch (Exception) { }
                    }
                    if (obj.Count == 0) throw new InvalidDataException("Not found");
                }
                if (query.ContainsKey("last"))
                {
                    query.TryGetValue("last", out string param);
                    if (!int.TryParse(param, out int iparam))
                        throw new FormatException("InvalidRequest: expected int - got " + param);
                    last = iparam;
                    if (last > newslist.Count || last < 0) throw new FormatException("InvalidRequest: value is out of range");
                    if (obj.Count > 0 && last > 0 && last <= obj.Count)
                        obj = obj.Take(last).ToList();
                    else if (last > 0 && last <= newslist.Count)
                        obj = parser.newslist.Take(last).ToList();
                }
                if (query.ContainsKey("after"))
                {
                    query.TryGetValue("after", out string param);
                    try
                    {
                        if (param.Contains(','))
                        {
                            string[] spar = param.Split(',');
                            if (int.TryParse(spar[0], out int countparam))
                            {
                                obj = newslist.TakeWhile(x => !x.Url.Contains(spar[1])).ToList();
                                int count = obj.Count - countparam;
                                if (count > 0 && count < obj.Count)
                                {
                                    obj = obj.Skip(count).ToList();
                                }
                                else
                                {
                                    if (spar.Count() == 2 || (spar.Count() == 3 && spar[2] == "true"))
                                        obj = new List<NewsItem>();
                                }
                            }
                        }
                        else
                        {
                            obj = newslist.TakeWhile(x => !x.Url.Contains(param)).ToList();
                        }
                    }
                    catch (Exception)
                    {
                        throw new FormatException("InvalidRequest  >> invalid format or it`s magic");
                    }
                }
                if (query.ContainsKey("before"))
                {
                    query.TryGetValue("before", out string param);
                    try
                    {
                        if (param.Contains(','))
                        {
                            string[] spar = param.Split(',');
                            if (int.TryParse(spar[0], out int count))
                            {
                                obj = newslist.SkipWhile(x => !x.Url.Contains(spar[1])).Skip(1).ToList();
                                if (count > obj.Count) count = obj.Count;
                                if (count > 0)
                                    obj = obj.Take(count).ToList();
                            }
                        }
                        else
                        {
                            obj = newslist.SkipWhile(x => !x.Url.Contains(param)).Skip(1).Take(10).ToList();
                        }
                    }
                    catch (Exception)
                    {
                        throw new FormatException("InvalidRequest >> invalid format or it`s magic");
                    }
                }
                if (query.ContainsKey("offset"))
                {
                    query.TryGetValue("offset", out string param);
                    try
                    {
                        if (param.Contains(','))
                        {
                            string[] spar = param.Split(',');
                            if (int.TryParse(spar[0], out int skip) && int.TryParse(spar[1], out int count))
                            {
                                obj = newslist.Skip(skip).Take(count).ToList();
                            }
                            else throw new FormatException("InvalidRequest >> Can`t parse count parameter");
                        }
                        else
                        {
                            obj = newslist.Skip(int.Parse(param)).Take(10).ToList();
                        }
                    }
                    catch (Exception)
                    {
                        throw new FormatException("InvalidRequest  >> invalid format or it`s magic");
                    }
                }

                if (!toHTML)
                {
                    foreach (var t in obj)
                    {
                        if (t.Detailed == null) throw new InvalidOperationException("Server is starting now");
                        t.Detailed.ContentHTML = parser.newslist.Where(x => x.Url == t.Url).First().GetText();
                    }
                }
                if (query.ContainsKey("inst"))
                {
                    var item = await ParsePageInstant(query["inst"], toHTML);
                    if (item.Item1 != null)
                    {
                        return API.CreateResponse(new List<NewsItem>(new[] { item.Item1 }), new DivideByZeroException(), item.Item2);
                    }
                    else throw new InvalidOperationException("Can`t get article. Reason: " + item.Item2.ToString());
                }

                if (obj.Count == 0) throw new InvalidDataException("Not found");
            }
            catch (Exception ex) { err = ex; }

            return CreateResponse(obj, err);
        }
        public async Task<string> SearchNews()
        {
            if (query.ContainsKey("query"))
            {
                query.TryGetValue("query", out string qpar);
                int count = 1;
                List<NewsItem> news = new List<NewsItem>();
                List<System.Threading.Thread> tr = new List<System.Threading.Thread>();
                try
                {
                    if (qpar.Contains(','))
                    {
                        var t = qpar.Split(',');
                        count = int.Parse(t[0]);
                        if (count > 1000) throw new FormatException("Freak: server might fall");
                        qpar = t[1];
                    }
                    else throw new FormatException("Expected count parameter. Search results may be very huge");
                    CreateClientRequest request = new CreateClientRequest("http://nuwm.edu.ua/search?text=" + qpar.Replace(' ', '+'));

                    var resp = await request.GetAsync();
                    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                    bool virg = true;

                    var wnode = doc.DocumentNode.Descendants().Where(x =>
                    x.Name == "div"
                    && x.HasClass("news") && x.HasClass("search") &&
                    x.GetAttributeValue("role", "") == "group");
                    if (!wnode.Any()) { throw new InvalidDataException("Not found"); }
                    var node = wnode.First();
                    foreach (var a in node.Elements("article"))
                    {
                        var btnf = a.Descendants("a").Where(x => x.HasClass("btn") && x.HasClass("s2"));
                        if (btnf.Any())
                        {
                            var link = btnf.First().GetAttributeValue("href", "");
                            if (link.Contains("/news"))
                            {
                                bool found = false;
                                foreach (var i in Server.CurrentParserPool.POOL.Values)
                                {
                                    var t = i.newslist.Where(x => x.Url == link);
                                    if (t.Count() == 1)
                                    {
                                        found = true;
                                        news.Add(t.First());
                                        break;
                                    }

                                }
                                if (InstantCache != null)
                                {
                                    var inst = InstantCache.Where(x => x.Url == link);
                                    if (inst.Count() == 1)
                                    {
                                        news.Add(inst.First());
                                        found = true;
                                    }
                                }
                                if (!found)
                                {
                                    var u = new NewsItem()
                                    {
                                        Excerpt = a.Descendants("p").First().InnerText,
                                        Title = a.Descendants("a").Where(x => x.HasClass("name")).First().InnerText,
                                        Url = link
                                    };

                                    tr.Add(new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(PageThread)));
                                    tr.Last().Start(u);
                                    news.Add(u);
                                    virg = false;
                                    if (InstantCache == null) InstantCache = new List<NewsItem>();
                                    InstantCache.Add(u);
                                }
                            }
                        }
                        if (news.Count == count) break;
                    }
                    while (true)
                    {
                        tr = tr.Where(x => x.IsAlive).ToList();
                        if (tr.Count == 0) break;
                    }
                    if (news.Count == 0)
                    {
                        return JsonConvert.SerializeObject(ResponseTyper(new InvalidDataException("Not Found")));
                    }


                    return JsonConvert.SerializeObject(ResponseTyper(null, news, (virg ? InstantState.FromCache : InstantState.Success)));
                }
                catch (Exception ex)
                {
                    return JsonConvert.SerializeObject(ResponseTyper(ex, news));
                }
            }
            return JsonConvert.SerializeObject(ResponseTyper(new InvalidOperationException("InvalidKey: query expected"), null));
        }

        private static Response ResponseTyper(Exception err, object obj = null, InstantState state = InstantState.Success)
        {
            Response resp;
            if (err == null)
            {
                resp = new Response()
                {
                    Code = StatusCode.Success,
                    Error = "null",
                    Cache = (state == InstantState.FromCache),
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
            else if (err != null && err.GetType() == typeof(DivideByZeroException))
            {
                resp = new Response() { Code = StatusCode.Success, Error = "null", Cache = (state == InstantState.FromCache), Content = obj };
            }
            else
            {
                resp = new Response() { Code = StatusCode.Undefined, Error = (err != null) ? err.Message + "\n" + err.StackTrace : "", Content = obj };
            }

            return resp;
        }

        public static string CreateResponse(List<NewsItem> obj, Exception err,
            InstantState state = InstantState.Success)
        {
            Response resp;
            if (err != null)
            {
                resp = ResponseTyper(err, obj, state);
            }
            else
            {
                resp = new Response()
                {
                    Code = StatusCode.Success,
                    Error = null,
                    Content = new NewsItemVisualizer()
                    {
                        NewsItemList = obj
                    }
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
        public async Task<string> GetById(int id)
        {
            var pool = Server.CurrentParserPool.POOL.Values.Where(x => x.InstituteID == id);
            if (pool.Count() == 1)
            {
                return await UniversalAsync(pool.First());
            }
            return null;
        }

        private static string Serialize(object data)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
            settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
            return JsonConvert.SerializeObject(data, settings);
        }
    }
}