using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JSON;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils;
using MaxRev.Servers.Utils.Filesystem;
using MaxRev.Servers.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUWM.Servers.Core.News;

namespace Lead
{
    public class CacheManager
    {
        private readonly ILogger _logger; 
        private readonly IServiceProvider _services;

        public CacheManager(ILogger logger, IServiceProvider services)
        {
            _logger = logger;
            _services = services;
        }

        public async Task LoadNewsCacheAsync(Parser parser)
        {
            var path = Path.Combine(
                MainApp.GetApp.DirectoryManager[MainApp.Dirs.Cache],
                "news_" + parser.Key + ".txt");

            try
            {
                if (File.Exists(path))
                {
                    using (var fl = File.OpenText(path))
                        await fl.ReadToEndAsync().ContinueWith(t =>
                        {
                            var r = JsonConvert.DeserializeObject<List<NewsItem>>(t.Result);
                            if (r != null)
                            {
                                parser.Newslist = r;
                            }
                        }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.NotifyError(LogArea.Other, ex);
            }
        }
        public async Task SaveCacheAsync(string key = "")
        {
            if (_parserPool != null)
            {
                if (key == "")
                {
                    foreach (var ParserX in _parserPool.Keys)
                    {
                        await SaverAsync(ParserX).ConfigureAwait(false);
                    }

                    await _parserPool.InstantCache.SaveInstantCacheAsync().ConfigureAwait(false);
                }
                else
                {
                    await SaverAsync(key).ConfigureAwait(false);
                }
            }
        }

        public ParserPool _parserPool => _services.GetRequiredService<ParserPool>();

        /// <exception cref="T:System.UnauthorizedAccessException">The caller does not have the required permission.</exception>
        /// <exception cref="T:System.IO.DirectoryNotFoundException">The specified path is invalid (for example, it is on an unmapped drive).</exception>
        public async Task SaverAsync(string ParserX)
        {
            var ig = _parserPool[ParserX];
            if (ig.Newslist != null && ig.Newslist.Count > 0)
            {
                using (var toper = File.CreateText(Path.Combine(
                    MainApp.GetApp.DirectoryManager[MainApp.Dirs.Cache], "news_" + ParserX + ".txt")))
                    await toper.WriteAsync(JsonConvert.SerializeObject(ig.Newslist)).ConfigureAwait(false);
            }
        }
    }
}