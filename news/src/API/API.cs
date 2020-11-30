using MaxRev.Servers.API.Controllers;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Utils;
using MaxRev.Utils.Methods;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUWEE.Servers.Core.News.Json;
using NUWEE.Servers.Core.News.Parsers;
using NUWEE.Servers.Core.News.Updaters;
using static NUWEE.Servers.Core.News.Updaters.InstantCacher;
using NUWM.Servers.Core.News.Json;

namespace NUWEE.Servers.Core.News.API
{
    [RouteBase("news/api")]
    internal class API : CoreApi
    {
        protected override void OnInitialized()
        {
            if (ModuleContext != default)
            {
                ModuleContext.StreamContext.KeepAlive = false;
                Builder.ContentType("text/plain");
            }
        }

        private ParserPool parserPool => Services.GetRequiredService<ParserPool>();
        private CacheManager _cacheManager => Services.GetRequiredService<CacheManager>();
        #region Invokers
        [Route("keys")]
        public string GetKeys()
        {
            return "API KEYS:" + string.Join('\n', parserPool.Keys.ToArray());
        }
        [Route("trace")]
        public string GetTrace()
        {
            //Server.State.DecApiResponseUser();
            var all = AllParsersLogger();
            return Tools.GetBaseTrace(ModuleContext) + $"\nAll articles count: " + all.Item2 + '\n' + all.Item1;
        }
        [Route("news_config")]
        private string JsonConfig()
        {
            return MainApp.Config.Serialize();
        }
        [Route("set")]
        public async Task<IResponseInfo> SettingTopAsync()
        {
            //Server.State.DecApiResponseUser();

            var Query = Info.Query;
            string FS, ContentType = "text/plain";
            if (Query.HasKey("saveinstcache"))
            {
                if (Query.HasKey("key"))
                {
                    if (Query["key"] == "all")
                    {
                        await _cacheManager.SaveCacheAsync().ConfigureAwait(false);
                        FS = "saved ALL";
                    }
                    else if (parserPool.ContainsKey(Query["key"]))
                    {
                        await _cacheManager.SaveCacheAsync(Query["key"]).ConfigureAwait(false);
                        FS = "saved " + Query["key"]; ContentType = "text/plain";

                    }
                    else
                    {
                        throw new FormatException("InvalidRequest: invalid key parameter");
                    }
                }
                else
                {
                    await parserPool.InstantCache.SaveInstantCacheAsync().ConfigureAwait(false);
                    FS = "saved";
                }
            }
            else
            {
                if (Query.HasKey("reparse"))
                {
                    parserPool.InitRun();
                    FS = "Reinit task started";
                }
                else
                {
                    FS = "Anauthorized";
                }
            }
            FS = !string.IsNullOrEmpty(FS) ? FS : "Context undefined";
            return
                Builder.Content(FS).ContentType(ContentType).Build();
        }

        [Route("searchNews")]
        public async Task<string> SearchNewsAsync()
        {
            var Query = Info.Query;
            try
            {
                if (Query.HasKey("query"))
                {
                    var query = Query["query"];

                    try
                    {
                        int count;
                        if (query.Contains(','))
                        {
                            var rawQuery = query.Split(',');
                            count = int.Parse(rawQuery[0]);
                            if (count > 1000)
                            {
                                throw new FormatException("Freak: server might fall");
                            }

                            query = rawQuery[1];
                        }
                        else
                        {
                            throw new FormatException("Expected count parameter. Search results may be very huge");
                        }


                        var search = Services.GetRequiredService<SearchService>();

                        var (result, virg) = await search.QueryAsync(query, count).ConfigureAwait(false);


                        if (result.Count == 0)
                        {
                            return ResponseTyper(new InvalidDataException("Not Found")).Serialize();
                        }


                        return ResponseTyper(null, result, virg ? InstantState.FromCache : InstantState.Success).Serialize();
                    }
                    catch (Exception ex)
                    {
                        return ResponseTyper(ex).Serialize();
                    }
                }
            }
            catch (Exception ex)
            {
                return ResponseTyper(ex).Serialize();
            }
            return ResponseTyper(new InvalidOperationException("InvalidKey: query expected")).Serialize();
        }

        /// <exception cref="T:System.OverflowException"><paramref>s</paramref> represents a number less than <see cref="System.Int32.MinValue"></see> or greater than <see cref="System.Int32.MaxValue"></see>.</exception> 
        [Route("getById")]
        public async Task<Response> GetByIdAsync()
        {
            var id = int.Parse(Info.Query["id"]);
            var pool = parserPool.Values.Where(x => x.InstituteID == id).ToArray();
            if (pool.Count() == 1)
            {
                return await UniversalAsync(pool.First()).ConfigureAwait(false);
            }
            return ResponseTyper(new Exception("Undefined ID"));
        }

        [Route("{key}")] // it's dynamic so it must be last in invoke list
        public async Task<Response> ProcessWithParserAsync(string key)
        {
            var pool = parserPool;
            if (pool.ContainsKey(key))
            {
                return await UniversalAsync(pool[key]).ConfigureAwait(false);
            }
            return ResponseTyper(new Exception("Undefined key"));

        }
        #endregion

        #region Service

        public async Task<Response> UniversalAsync(AbstractParser parser)
        {
            var Query = Info.Query;

            var newslist = parser.Newslist.ToList();
            Exception err = null;
            var toHTML = false;
            int last;
            var obj = new List<NewsItem>();
            try
            {
                if (newslist.Count == 0)
                    throw new InvalidOperationException("Server is starting now");

                if (Query.HasKey("reparse"))
                {
                    if (Query["reparse"] == "true")
                    {
                        parserPool.Schedulers[parser.Key].ReparseTask();
                    }
                }
                if (Query.HasKey("html"))
                {
                    var param = Query["html"];
                    if (!int.TryParse(param, out var iparam))
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

                    var param = Query["uri"];

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
                    if (obj.Count > 0)
                    {
                        throw new FormatException("InvalidRequest  >> query must be unique in request");
                    }

                    var param = Query["query"];

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
                    if (obj.Count > 0)
                    {
                        throw new FormatException("InvalidRequest  >> uriquery must be unique in request");
                    }

                    var param = Query["uriquery"];

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
                    var param = Query["last"];
                    if (!int.TryParse(param, out var iparam))
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
                    if (obj.Count == 0)
                    {
                        throw new InvalidDataException("Not found");
                    }
                }
                if (Query.HasKey("after"))
                {
                    var param = Query["after"];
                    try
                    {
                        if (param.Contains(','))
                        {
                            var spar = param.Split(',');
                            if (int.TryParse(spar[0], out var countparam))
                            {
                                obj = newslist.TakeWhile(x => !x.Url.Contains(spar[1])).ToList();
                                var count = obj.Count - countparam;
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
                    catch (Exception ex)
                    {
                        throw new FormatException("InvalidRequest  >> invalid format or it`s magic", ex);
                    }
                    if (obj.Count == 0)
                    {
                        throw new InvalidDataException("Not found");
                    }
                }
                if (Query.HasKey("before"))
                {
                    var param = Query["before"];
                    try
                    {
                        if (param.Contains(','))
                        {
                            var spar = param.Split(',');
                            if (int.TryParse(spar[0], out var count))
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
                    catch (Exception ex)
                    {
                        throw new FormatException("InvalidRequest >> invalid format or it`s magic", ex);
                    }
                    if (obj.Count == 0)
                    {
                        throw new InvalidDataException("Not found");
                    }
                }
                if (Query.HasKey("offset"))
                {
                    var param = Query["offset"];
                    try
                    {
                        if (param.Contains(','))
                        {
                            var spar = param.Split(',');
                            if (int.TryParse(spar[0], out var skip) && int.TryParse(spar[1], out var count))
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
                    catch (Exception ex)
                    {
                        throw new FormatException("InvalidRequest  >> invalid format or it`s magic", ex);
                    }
                }
                else
                {
                    if (obj.Count == 0)
                        obj = newslist.Take(10).ToList();
                }

                if (!toHTML)
                {
                    foreach (var t in obj)
                    {
                        if (t.Detailed == null)
                        {
                            throw new InvalidOperationException($"Server is starting now. Progress of parser: {newslist.Count(x => x.Detailed != null)}/{newslist.Count}");
                        }

                        t.Detailed.ContentHTML = parser.Newslist.First(x => x.Url == t.Url).GetText();
                    }
                }
                if (Query.HasKey("inst"))
                {
                    var item = await parserPool.InstantCache.ParsePageInstantAsync(Query["inst"], toHTML).ConfigureAwait(false);
                    if (item.Item1 != null)
                    {
                        return CreateResponse(new List<NewsItem>(new[] { item.Item1 }), new DivideByZeroException(), item.Item2);
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

            newslist.Clear();

            return CreateResponse(obj, err, InstantState.FromCache);
        }

        private Tuple<string, int> AllParsersLogger()
        {
            var resp = "";
            var countAllnews = 0;
            foreach (AbstractParser parser in parserPool.Values.OrderByDescending(x => x.Newslist?.Count))
            {
                TimeSpan k = default;

                PoolParserScheduler scheduler = default;
                if (parserPool.Schedulers.ContainsKey(parser.Key)
                    && parserPool.Schedulers[parser.Key] != default)
                {
                    scheduler = parserPool.Schedulers[parser.Key];
                    k = scheduler.ScheduledTime - TimeChron.GetRealTime();
                }

                resp += "\n" + new string('-', 20);
                if (parser.Newslist != null && parser.Newslist.Count > 0)
                {
                    resp += $"\nParser: {parser.Key}";
                    // resp += $"\nObject size: {ert.Size()}";
                    resp += $"\nNews Articles: {parser.Newslist?.Count}";
                    if (Math.Abs(k.TotalSeconds) < 0.00001)
                    {
                        resp += "\nCache is updating now!";
                    }
                    else
                    {
                        var u = scheduler?.ScheduledTime ?? TimeChron.GetRealTime();
                        var dx = $"{u.ToLongTimeString()} {u.ToShortDateString()}";
                        resp += $"\nNext parsing in: {k.Days}d {k.Hours}h {k.Minutes}m {k.Seconds}s ({dx})";
                    }

                    resp += $"\nCache epoch: { parser.CacheEpoch }";
                }
                else
                {
                    resp += $"\nParser {parser.Key}  not ready now";
                    resp += $"\nNext atempt to parse in: {k.Days}d {k.Hours}h {k.Minutes}m {k.Seconds}s";
                }
                resp += "\n" + new string('-', 20) + "\n";
                countAllnews += parser.Newslist?.Count ?? 0;
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
                resp = new Response
                {
                    Code = StatusCode.Success,
                    Error = "null",
                    Cache = (state == InstantState.FromCache),
                    Content = obj
                };
            }
            else
            if (err is FormatException)
            {
                resp = new Response { Code = StatusCode.InvalidRequest, Error = (err.Message + "\n" + Tools.AnonymizeStack(err.StackTrace)).Trim(), Content = null };
            }
            else if (err is InvalidOperationException)
            {
                resp = new Response { Code = StatusCode.ServerSideError, Error = err.Message, Content = null };
            }
            else if (err is EntryPointNotFoundException)
            {
                resp = new Response { Code = StatusCode.DeprecatedMethod, Error = err.Message, Content = null };
            }
            else if (err is InvalidDataException)
            {
                resp = new Response { Code = StatusCode.NotFound, Error = err.Message, Content = null };
            }
            else if (err is DivideByZeroException)
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

        public virtual Response CreateResponse(List<NewsItem> obj, Exception err,
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

            return resp;
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

        #endregion
    }
}