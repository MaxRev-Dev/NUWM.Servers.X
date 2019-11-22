using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Servers.API;
using MaxRev.Servers.API.Response;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.News
{
    [RouteBase("api")]
    internal class API : CoreApi
    { 
        ParserPool pool => (ParserPool)Services.GetService(typeof(ParserPool));
        #region Invokers
        [Route("keys")]
        private string GetKeys()
        {
            return "API KEYS:" + string.Join('\n', ParserPool.Current.POOL.Keys.ToArray());
        }
        [Route("trace")]
        private string GetTrace()
        {
            var all = AllParsersLogger();
            return Tools.GetBaseTrace(Server) + "\nAll articles count: " + all.Item2 + '\n' + all.Item1;
        }

        [Route("set")]
        public async Task<Tuple<string, string>> SettingTop()
        {

            var Query = Info.Query;
            string FS, ContentType = "text/plain";
            if (Query.HasKey("saveinstcache"))
            {
                if (Query.HasKey("key"))
                {
                    if (Query["key"] == "all")
                    {
                        await App.Get.ParserPool.SaveCache();
                        FS = "saved ALL";
                    }
                    else if (ParserPool.Current.POOL.ContainsKey(Query["key"]))
                    {
                        await App.Get.ParserPool.SaveCache(Query["key"]);
                        FS = "saved " + Query["key"]; ContentType = "text/plain";

                    }
                    else
                    {
                        throw new FormatException("InvalidRequest: invalid key parameter");
                    }
                }
                else
                {
                    App.Get.ParserPool.SaveInstantCache();
                    FS = "saved";
                }
            }
            else
            {
                if (Query.HasKey("reparse"))
                {
                    ParserPool.Current.BaseInitParsers(Services.GetRequiredService<NewsConfig>());
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
            try
            {
                if (Query.HasKey("query"))
                {
                    string qpar = Query["query"];
                    int count;
                    var news = new List<NewsItem>();
                    var tr = new List<Task>();
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
                        bool virg = true;
                        var rm = await RequestAllocator.Instance.UsingPool(new Request("http://nuwm.edu.ua/search?text=" + qpar.Replace(' ', '+')));

                        HtmlDocument doc = new HtmlDocument();

                        doc.Load(await rm.Content.ReadAsStreamAsync());

                        var wnode = doc.DocumentNode.Descendants().Where(x =>
                        x.Name == "div"
                        && x.HasClass("news") && x.HasClass("search") &&
                        x.GetAttributeValue("role", "") == "group");
                        var nodes = wnode as HtmlNode[] ?? wnode.ToArray();
                        if (!nodes.Any()) { throw new InvalidDataException("Not found"); }
                        var node = nodes.First();
                        foreach (var a in node.Elements("article"))
                        {
                            var btnf = a.Descendants("a").Where(x => x.HasClass("btn") && x.HasClass("s2"));
                            var htmlNodes = btnf as HtmlNode[] ?? btnf.ToArray();
                            if (htmlNodes.Any())
                            {
                                var link = htmlNodes.First().GetAttributeValue("href", "");
                                if (link.Contains("/news"))
                                {
                                    bool found = false;
                                    foreach (var i in ParserPool.Current.POOL.Values)
                                    {
                                        var t = i.Newslist.Where(x => x.Url == link).ToArray();
                                        if (t.Length == 1)
                                        {
                                            found = true;
                                            news.Add(t.First());
                                            break;
                                        }

                                    }
                                    if (pool.InstantCache != null)
                                    {
                                        var inst = pool.InstantCache.Where(x => x.Url == link).ToArray();
                                        if (inst.Length == 1)
                                        {
                                            news.Add(inst.First());
                                            found = true;
                                        }
                                    }
                                    if (!found)
                                    {
                                        var u = new NewsItem
                                        {
                                            Excerpt = a.Descendants("p").First().InnerText,
                                            Title = a.Descendants("a").First(x => x.HasClass("name")).InnerText,
                                            Url = link
                                        };

                                        tr.Add(Task.Run(() => NewsItemDetailed.Process(u)));
                                        news.Add(u);
                                        if (pool.InstantCache == null)
                                        {
                                            pool.InstantCache = new List<NewsItem>();
                                        }
                                        virg = false;
                                        pool.InstantCache.Add(u);
                                    }
                                }
                            }
                            if (news.Count == count)
                            {
                                break;
                            }
                        }

                        await Task.WhenAll(tr);

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
            return JsonConvert.SerializeObject(ResponseTyper(new InvalidOperationException("InvalidKey: query expected")));
        }

        [Route("getById/{id}")]
        public async Task<string> GetById(int id)
        {
            var poolx = pool.POOL.Values.Where(x => x.InstituteID == id).ToArray();
            if (poolx.Length == 1)
            {
                return await UniversalAsync(poolx.First());
            }
            return null;
        }

        [Route("{key}")] // it's dynamic so it must be last in invoke list
        public async Task<string> ProcessWithParser(string key)
        {
            var poolx = pool.POOL;
            if (poolx.ContainsKey(key))
            {
                return await UniversalAsync(poolx[key]);
            }
            return default;

        }
        #endregion

        #region Service
        public async Task<string> UniversalAsync(Parser parser)
        {
            var Query = Info.Query;

            var newslist = Parser.DeepCopy(parser.Newslist);
            Exception err = null;
            bool toHTML = false;
            List<NewsItem> obj = newslist;
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

                    toHTML = iparam == 1;
                }
                if (Query.HasKey("uri"))
                {
                    if (obj.Count > 0)
                    {
                        throw new FormatException("InvalidRequest  >> uri & id");
                    }

                    string param = Query["uri"];

                    var c = newslist.Where(x => x.Url == param).ToArray();
                    if (c.Any())
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
                    if (obj.Any())
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
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                    if (obj.Count == 0)
                    {
                        throw new InvalidDataException("Not found");
                    }
                }
                if (Query.HasKey("uriquery"))
                {
                    if (obj.Any())
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
                        catch (Exception)
                        {
                            // ignored
                        }
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

                    var last = iparam;
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

                        t.Detailed.ContentHTML = parser.Newslist.First(x => x.Url == t.Url).GetText();
                    }
                }
                if (Query.HasKey("inst"))
                {
                    var item = await pool.ParsePageInstant(Query["inst"], toHTML);
                    if (item.Item1 != null)
                    {
                        return CreateResponse(new List<NewsItem>(new[] { item.Item1 }), new DivideByZeroException(), item.Item2);
                    }

                    throw new InvalidOperationException("Can`t get article. Reason: " + item.Item2);
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
            foreach (var ert in ParserPool.Current.POOL.Values.OrderByDescending(x => x.Newslist?.Count))
            {
                TimeSpan k = new TimeSpan();
                if (ert.scheduler != null)
                {
                    k = ert.scheduler.ScheduledTime - TimeChron.GetRealTime();
                }

                resp += "\n" + new string('-', 20);
                if (ert.Newslist != null && ert.Newslist.Count > 0)
                {
                    resp += $"\nParser: {ert.xkey}";
                    resp += $"\nNews Articles: {ert.Newslist?.Count}";
                    if (Math.Abs(k.TotalSeconds) < 0.001)
                    {
                        resp += "\nCache is updating now!";
                    }
                    else
                    {
                        resp += $"\nNext parsing in: {k.Days}d {k.Hours}h {k.Minutes}m {k.Seconds}s";
                    }

                    resp += $"\nCache epoch: {(ert.scheduler != null ? ert.CacheEpoch : 0)}";
                }
                else
                {
                    resp += $"\nParser {ert.xkey}  not ready now";
                    resp += $"\nNext atempt to parse in: {k.Days}d {k.Hours}h {k.Minutes}m {k.Seconds}s";
                }
                resp += "\n" + new string('-', 20) + "\n";
                countAllnews += ert.Newslist?.Count ?? 0;
            }
            return new Tuple<string, int>(resp, countAllnews);
        }
        #endregion

        #region ErrorHandling
        private Response ResponseTyper(Exception err, object obj = null, InstantState state = InstantState.Success)
        {
            Response resp;
            if (err == null)
            {
                resp = new Response
                {
                    Code = StatusCode.Success,
                    Error = "null",
                    Cache = state == InstantState.FromCache,
                    Content = obj
                };
            }
            else
            if (err.GetType() == typeof(FormatException))
            {
                resp = new Response { Code = StatusCode.InvalidRequest, Error = err.Message + "\n" + Tools.AnonymizeStack(err.StackTrace), Content = null };
            }
            else if (err.GetType() == typeof(InvalidOperationException))
            {
                resp = new Response { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(EntryPointNotFoundException))
            {
                resp = new Response { Code = StatusCode.DeprecatedMethod, Error = err.Message, Content = null };
            }
            else if (
                err is InvalidDataException)
            {
                resp = new Response { Code = StatusCode.NotFound, Error = err.Message, Content = null };
            }
            else if (err.GetType() == typeof(DivideByZeroException))
            {
                resp = new Response
                {
                    Code = StatusCode.Success,
                    Error = null,
                    Cache = state == InstantState.FromCache,
                    Content = new NewsItemVisualizer
                    {
                        NewsItemList = obj as List<NewsItem>
                    }
                };
            }
            else
            {
                resp = new Response { Code = StatusCode.Undefined, Error = err.Message + "\n" + Tools.AnonymizeStack(err.StackTrace), Content = obj };
            }

            return resp;
        }

        public string CreateResponse(List<NewsItem> obj, Exception err,
             InstantState state = InstantState.Success)
        {
            Response resp;
            if (err != null)
            {
                resp = ResponseTyper(err, obj, state);
            }
            else
            {
                resp = new Response
                {
                    Code = StatusCode.Success,
                    Error = null,
                    Content = new NewsItemVisualizer
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
                resp = new Response
                {
                    Code = StatusCode.Success,
                    Error = null,
                    Content = obj
                };
            }
            return JsonConvert.SerializeObject(resp);
        }

        public string CreateErrorResp(Exception err)
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

        #endregion
    }
}