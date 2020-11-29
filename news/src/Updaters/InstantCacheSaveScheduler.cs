using System;
using System.IO;
using MaxRev.Utils.Schedulers;
using Newtonsoft.Json;

namespace NUWEE.Servers.Core.News.Updaters
{
    [Serializable]
    public class InstantCacheSaveScheduler : BaseScheduler
    {
        private readonly InstantCacher _cacher;
        public InstantCacheSaveScheduler(InstantCacher cacher)
        {
            _cacher = cacher;
            CurrentWorkHandler = SaveInstantCache;
        }

        /// <exception cref="T:System.UnauthorizedAccessException">The caller does not have the required permission.</exception>
        public async void SaveInstantCache()
        {
            using (var g = File.CreateText(_cacher.InstantCachePath))
                await g.WriteAsync(JsonConvert.SerializeObject(_cacher.InstantCacheList)).ConfigureAwait(false);
        }
    }
}