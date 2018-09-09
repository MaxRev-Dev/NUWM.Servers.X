using JSON;
using MR.Servers;
using MR.Servers.Core.Request.Query;
using MR.Servers.Core.Route.Attributes;
using MR.Servers.Utils;
using Newtonsoft.Json;
using NUWM.Servers.Core.News;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Lead.ParserPool.Parser;

namespace APIUtilty
{
    [RouteBase("api")]
    internal class API : CoreAPI
    { 

        #region Invokers
        [Route("keys")]
        private string GetKeys()
        {
            return "API KEYS:" + string.Join('\n', Lead.ParserPool.Current.POOL.Keys.ToArray());
        }
        [Route("trace")]
        private string GetTrace()
        {
            ((Server)Server).State.DecApiResponsesUser();
            var all = AllParsersLogger();
            return Tools.GetBaseTrace(MainApp.GetApp.Core) + "\nAll articles count: " + all.Item2 +'\n'+ all.Item1;
        }

        [Route("set")]
        public async Task<Tuple<string, string>> SettingTop()
        {
            ((Server)Server).State.DecApiResponsesUser();

            var Query = Info.Query;
            var headers = Info.Headers;
            string FS = "", ContentType = "text/plain";
            if (Query.HasKey("saveinstcache"))
            {
                if (Query.HasKey("key"))
                {
                    if (Query["key"] == "all")
                    {
                        await SaveCache();
                        FS = "saved ALL";
                    }
                    else if (Lead.ParserPool.Current.POOL.ContainsKey(Query["key"]))
                    {
                        await SaveCache(Query["key"]);
                        FS = "saved " + Query["key"]; ContentType = "text/plain";

                    }
                    else
                    {
                        throw new FormatException("InvalidRequest: invalid key parameter");
                    }
                }
                else
                {
                    ScheduleInstantCacheSave.SaveInstantCache();
                    FS = "saved";
                }
            }
            else
            {
                if (Query.HasKey("reparse"))
                {
                    Lead.ParserPool.Current.BaseInitParsers();
                    FS = "Reinit task started";
                }
                else
                {
                    FS = "Anauthorized";
                }
            }
            FS = !string.IsNullOrEmpty(FS) ? FS : "Context undefined";
            return new Tuple<string, string>(FS, ContentType);
        }

        [Route("searchNews")]
        public async Task<string> SearchNewsAsync()
        {
            var Query = Info.Query;
            var headers = Info.Headers;
            try
            {
                if (Query.HasKey("query"))
                {
                    string qpar = Query["query"];
                    int count = 1;
                    List<NewsItem> news = new List<NewsItem>();
                    List<System.Threading.Thread> tr = new List<System.Threading.Thread>();
                    try
                    {
                        if (qpar.Contains(','))
                        {
                            var t = qpar.Split(',');
                            count = int.Parse(t[0]);
                            if (count > 1000)
                            {
                                throw new FormatException("Freak: server might fall");
                            }

                            qpar = t[1];
                        }
                        else
                        {
                            throw new FormatException("Expected count parameter. Search results may be very huge");
                        }

                        CreateClientRequest request = new CreateClientRequest("http://nuwm.edu.ua/search?text=" + qpar.Replace(' ', '+'));

                        HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();

                        var rm = await request.GetAsync();
                        doc.Load(await rm.Content.ReadAsStreamAsync());
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
                                    foreach (var i in Lead.ParserPool.Current.POOL.Values)
                                    {
                                        var t = i.Newslist.Where(x => x.Url == link);
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
                                        if (InstantCache == null)
                                        {
                                            InstantCache = new List<NewsItem>();
                                        }

                                        InstantCache.Add(u);
                                    }
                                }
                            }
                            if (news.Count == count)
                            {
                                break;
                            }
                        }
                        while (true)
                        {
                            tr = tr.Where(x => x.IsAlive).ToList();
                            if (tr.Count == 0)
                            {
                                break;
                            }
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
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(ResponseTyper(ex));
            }
            return JsonConvert.SerializeObject(ResponseTyper(new InvalidOperationException("InvalidKey: query expected"), null));
        }

        [Route("getById/{id}")]
        public async Task<string> GetById(int id)
        {
            var pool = Lead.ParserPool.Current.POOL.Values.Where(x => x.InstituteID == id);
            if (pool.Count() == 1)
            {
                return await UniversalAsync(pool.First());
            }
            return null;
        }

        [Route("{key}")] // it's dynamic so it must be last in invoke list
        public async Task<string> ProcessWithParser(string key)
        {
            var Query = Info.Query;
            var headers = Info.Headers; 

            var pool = Lead.ParserPool.Current.POOL;
            if (pool.ContainsKey(key))
            {
                return await UniversalAsync(pool[key]);      
            }
            return default;

        }
        #endregion

        #region Service
        public async Task<string> UniversalAsync(Lead.ParserPool.Parser parser)
        { 
            var Query = Info.Query;
            var headers = Info.Headers;

            var newslist = DeepCopy(parser.Newslist);
            Exception err = null;
            bool toHTML = false;
            int last = -1;
            List<NewsItem> obj = new List<NewsItem>();
            try
            {
                if (Query.HasKey("reparse"))
                {
                    if (Query["reparse"] == "true")
                    {
                        parser.scheduler.ReparseTask().Start();
                    }
                }
                if (Query.HasKey("html"))
                {
                    var param = Query["html"];
                    if (!int.TryParse(param, out int iparam))
                    {
                        throw new FormatException("InvalidRequest: expected 1/0 - got " + param);
                    }

                    toHTML = (iparam == 1 ? true : false);
                }
                if (Query.HasKey("uri"))
                {
                    if (obj.Count() > 0)
                    {
                        throw new FormatException("InvalidRequest  >> uri & id");
                    }

                    string param = Query["uri"];

                    var c = newslist.Where(x => x.Url == param);
                    if (c.Count() > 0)
                    {
                        obj.Add(c.First());
                    }
                    else
                    {
                        throw new InvalidDataException("Not found");
                    }
                }
                if (Query.HasKey("query"))
                {
                    if (obj.Count() > 0)
                    {
                        throw new FormatException("InvalidRequest  >> query must be unique in request");
                    }

                    string param = Query["query"];

                    foreach (var t in newslist)
                    {
                        try
                        {
                            if (t.Detailed.ContentHTML.Contains(param))
                            {
                                obj.Add(t);
                            }
                        }
                        catch (Exception) { }
                    }
                    if (obj.Count == 0)
                    {
                        throw new InvalidDataException("Not found");
                    }
                }
                if (Query.HasKey("uriquery"))
                {
                    if (obj.Count() > 0)
                    {
                        throw new FormatException("InvalidRequest  >> uriquery must be unique in request");
                    }

                    string param = Query["uriquery"];

                    foreach (var t in newslist)
                    {
                        try
                        {
                            if (t.Url.Contains(param))
                            {
                                obj.Add(t);
                            }
                        }
                        catch (Exception) { }
                    }
                    if (obj.Count == 0)
                    {
                        throw new InvalidDataException("Not found");
                    }
                }
                if (Query.HasKey("last"))
                {
                    string param = Query["last"];
                    if (!int.TryParse(param, out int iparam))
                    {
                        throw new FormatException("InvalidRequest: expected int - got " + param);
                    }

                    last = iparam;
                    if (last > newslist.Count || last < 0)
                    {
                        throw new FormatException("InvalidRequest: value is out of range");
                    }

                    if (obj.Count > 0 && last > 0 && last <= obj.Count)
                    {
                        obj = obj.Take(last).ToList();
                    }
                    else if (last > 0 && last <= newslist.Count)
                    {
                        obj = parser.Newslist.Take(last).ToList();
                    }
                }
                if (Query.HasKey("after"))
                {
                    string param = Query["after"];
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
                                    {
                                        obj = new List<NewsItem>();
                                    }
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
                if (Query.HasKey("before"))
                {
                    string param = Query["before"];
                    try
                    {
                        if (param.Contains(','))
                        {
                            string[] spar = param.Split(',');
                            if (int.TryParse(spar[0], out int count))
                            {
                                obj = newslist.SkipWhile(x => !x.Url.Contains(spar[1])).Skip(1).ToList();
                                if (count > obj.Count)
                                {
                                    count = obj.Count;
                                }

                                if (count > 0)
                                {
                                    obj = obj.Take(count).ToList();
                                }
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
                if (Query.HasKey("offset"))
                {
                    string param = Query["offset"];
                    try
                    {
                        if (param.Contains(','))
                        {
                            string[] spar = param.Split(',');
                            if (int.TryParse(spar[0], out int skip) && int.TryParse(spar[1], out int count))
                            {
                                obj = newslist.Skip(skip).Take(count).ToList();
                            }
                            else
                            {
                                throw new FormatException("InvalidRequest >> Can`t parse count parameter");
                            }
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
                        if (t.Detailed == null)
                        {
                            throw new InvalidOperationException("Server is starting now");
                        }

                        t.Detailed.ContentHTML = parser.Newslist.Where(x => x.Url == t.Url).First().GetText();
                    }
                }
                if (Query.HasKey("inst"))
                {
                    var item = await ParsePageInstant(Query["inst"], toHTML);
                    if (item.Item1 != null)
                    {
                        return API.CreateResponse(new List<NewsItem>(new[] { item.Item1 }), new DivideByZeroException(), item.Item2);
                    }
                    else
                    {
                        throw new InvalidOperationException("Can`t get article. Reason: " + item.Item2.ToString());
                    }
                }

                if (obj.Count == 0)
                {
                    throw new InvalidDataException("Not found");
                }
            }
            catch (Exception ex) { err = ex; }

            return CreateResponse(obj, err, InstantState.FromCache);
        }

        private Tuple<string, int> AllParsersLogger()
        {
            string resp = "";
            int countAllnews = 0;
            foreach (var ert in Lead.ParserPool.Current.POOL.Values.OrderByDescending(x => x.Newslist?.Count))
            {
                TimeSpan k = new TimeSpan();
                if (ert.scheduler != null)
                {
                    k = ert.scheduler.scheduledTime - TimeChron.GetRealTime();
                }

                resp += "\n" + new string('-', 20);
                if (ert.Newslist != null && ert.Newslist.Count > 0)
                {
                    resp += string.Format("\nParser: {0}", ert.xkey);
                    resp += string.Format("\nNews Articles: {0}", (ert.Newslist != null ? ert.Newslist.Count : 0));
                    if (k.TotalSeconds == 0)
                    {
                        resp += "\nCache is updating now!";
                    }
                    else
                    {
                        resp += string.Format("\nNext parsing in: {0}d {1}h {2}m {3}s", k.Days, k.Hours, k.Minutes, k.Seconds);
                    }

                    resp += string.Format("\nCache epoch: {0}", (ert.scheduler != null ? ert.CacheEpoch : 0));
                }
                else
                {
                    resp += string.Format("\nParser {0}  not ready now", ert.xkey);
                    resp += string.Format("\nNext atempt to parse in: {0}d {1}h {2}m {3}s", k.Days, k.Hours, k.Minutes, k.Seconds);
                }
                resp += "\n" + new string('-', 20) + "\n";
                countAllnews += (ert.Newslist != null ? ert.Newslist.Count : 0);
            }
            return new Tuple<string, int>(resp, countAllnews);
        }
        #endregion
        
        #region ErrorHandling
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
            {
                resp = new Response() { Code = StatusCode.InvalidRequest, Error = err.Message + "\n" + Tools.AnonymizeStack(err.StackTrace), Content = null };
            }
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
            else if (err != null && err.GetType() == typeof(DivideByZeroException))
            {
                resp = new Response()
                {
                    Code = StatusCode.Success,
                    Error = null,
                    Cache = (state == InstantState.FromCache),
                    Content = new NewsItemVisualizer()
                    {
                        NewsItemList = obj as List<NewsItem>
                    }
                };
            }
            else
            {
                resp = new Response() { Code = StatusCode.Undefined, Error = (err != null) ? err.Message + "\n" + Tools.AnonymizeStack(err.StackTrace) : "", Content = obj };
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
                    },
                    Cache = state == InstantState.FromCache
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

        #endregion
    }
}