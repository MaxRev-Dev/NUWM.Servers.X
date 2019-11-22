using System;
using System.IO;
using MaxRev.Utils.Schedulers;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.News
{
    public class InstantCacheSaveScheduler : BaseScheduler
    {
        private readonly ParserPool pool;

        public InstantCacheSaveScheduler(ParserPool pool) : base(TimeSpan.FromHours(1))
        {
            this.pool = pool;
        }

        protected override async void OnTimerElapsed()
        {
            using (var file = File.CreateText(pool.InstCachePath))
            {
                await file.WriteAsync(JsonConvert.SerializeObject(pool.InstantCache));
            }
        }
    }
}