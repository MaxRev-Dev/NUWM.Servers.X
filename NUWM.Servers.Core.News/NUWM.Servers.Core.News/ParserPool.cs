using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Servers.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;
using NUWM.Servers.Core.News;

namespace Lead
{
    [Serializable]
    public class ParserPool : IDictionary<string, Parser>
    {
        public static string site_url = "http://nuwm.edu.ua",
            site_abit_url = "http://start.nuwm.edu.ua";

        private readonly ILogger _logger;
        private readonly IServiceProvider _services;
        private readonly CacheManager _cacheManager;
        public ParserPool(IServiceProvider services, ILogger logger, CacheManager cacheManager, InstantCacheSaveScheduler instantCacheScheduler)
        {
            _services = services;
            _logger = logger;
            _cacheManager = cacheManager;
            _instantCacheScheduler = instantCacheScheduler;
            _instantCacheScheduler.ScheduleTimer();
            InitRun();
        }


        private ConcurrentDictionary<string, Parser> _internalPool { get; }
             = new ConcurrentDictionary<string, Parser>();
        internal Dictionary<string, PoolParserScheduler> Schedulers { get; }
            = new Dictionary<string, PoolParserScheduler>();
        internal InstantCacher InstantCache => _services.GetRequiredService<InstantCacher>();
        private InstantCacheSaveScheduler _instantCacheScheduler { get; }


        public void InitRun()
        {
            int offset = 0;
            int newsOffset = 0;
            InstantCache.Load();
            try
            {
                foreach (var s in MainApp.Config.Urls)
                {
                    string news_url = s.Url;
                    int unid = s.InstituteID;
                    string key = null; bool abit = false;
                    if (news_url.Contains("start.nuwm.edu.ua"))
                    {
                        if (news_url.Contains("kolonka-novyn"))
                        {
                            key = "abit-news";
                        }
                        else if (news_url.Contains("oholoshennia"))
                        {
                            key = "abit-ads";
                        }

                        abit = true;
                    }
                    else if (!news_url.Contains("university"))
                    {
                        var p = news_url.Substring(0, news_url.LastIndexOf('/'));
                        key = p.Substring(p.LastIndexOf('/') + 1);
                    }
                    else
                    {
                        key = news_url.Substring(news_url.LastIndexOf('/') + 1);
                    }

                    if (key != null)
                    {
                        if (key.Equals("nuwm.edu.ua"))
                        {
                            key = string.Join("", news_url.Substring(news_url.LastIndexOf('/') + 1).Split(new char[] { '-', '_' }).Select(x => x[0].ToString()));
                        }

                        if (key.Contains("zaochno-distanc"))
                        {
                            key = "zdn";
                        }
                    }
                    var offsetcalc = unid == -100 ?
                        newsOffset += MainApp.Config.ParserOffsetMinutes :
                        offset += MainApp.Config.ParserOffsetMinutes;
                    var parser = _services.GetRequiredService<ParserFactory>().GetParser(news_url, key, abit, unid);
                    parser.CacheEpoch = 0;

                    if (!Schedulers.ContainsKey(parser.Key))
                    {
                        var sc = _services.GetRequiredService<PoolParserScheduler>();
                        sc.WithParameters(parser, new TimeSpan(0, offsetcalc, 0)); 
                        sc.ScheduleTimer();
                        Schedulers[parser.Key] = sc;
                    }
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (!_internalPool.TryAdd(key, parser))
                            {
                                await _cacheManager.LoadNewsCacheAsync(parser).ConfigureAwait(false);
                                _internalPool[key] = parser;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.NotifyError(LogArea.Other, ex);
                        }
                        finally
                        {
                            if (parser.Newslist.Count == 0)
                            {
                                await _cacheManager.LoadNewsCacheAsync(parser).ConfigureAwait(false);
                            }

                            RunParseThread(parser);

                        }
                    });
                }
            }
            catch (Exception ex) { _logger.NotifyError(LogArea.Other, ex); }
        }
        private void RunParseThread(Parser parser)
        {
            try
            {
                Task.Run(() => parser.ParsePagesAsync(parser.Url));
            }
            catch
            {
                // ignored
            }
        }

        #region IDictionaryImpl

        public IEnumerator<KeyValuePair<string, Parser>> GetEnumerator()
        {
            return _internalPool.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_internalPool).GetEnumerator();
        }

        /// <exception cref="T:System.OverflowException">The dictionary already contains the maximum number of elements (<see cref="System.Int32.MaxValue"></see>).</exception>
        public void Add(KeyValuePair<string, Parser> item)
        {
            _internalPool.TryAdd(item.Key, item.Value);
        }

        public void Clear()
        {
            _internalPool.Clear();
        }

        public bool Contains(KeyValuePair<string, Parser> item)
        {
            return _internalPool.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, Parser>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, Parser> item)
        {
            return _internalPool.TryRemove(item.Key, out _);
        }

        public int Count => _internalPool.Count;

        public bool IsReadOnly => false;

        /// <exception cref="T:System.OverflowException">The dictionary already contains the maximum number of elements (<see cref="System.Int32.MaxValue"></see>).</exception>
        public void Add(string key, Parser value)
        {
            _internalPool.TryAdd(key, value);
        }

        public bool ContainsKey(string key)
        {
            return _internalPool.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return _internalPool.TryRemove(key, out _);
        }

        public bool TryGetValue(string key, out Parser value)
        {
            return _internalPool.TryGetValue(key, out value);
        }

        public Parser this[string key]
        {
            get => _internalPool[key];
            set => _internalPool[key] = value;
        }

        public ICollection<string> Keys => _internalPool.Keys;

        public ICollection<Parser> Values => _internalPool.Values;


        #endregion
    }
}