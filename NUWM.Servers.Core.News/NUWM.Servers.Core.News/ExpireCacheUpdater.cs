using System;
using System.Threading.Tasks;
using MaxRev.Utils;
using MaxRev.Utils.Schedulers;

namespace NUWM.Servers.Core.News
{
    public class ExpireCacheUpdater : BaseScheduler
    {
        public ExpireCacheUpdater() : base(new TimeSpan(1, 0, 0))
        {
        }

        public void CheckForUpdates()
        {
            foreach (var i in ParserPool.Current.POOL.Values)
            {
                Task.Run(() => UpdateParser(i));
            }
        }
        private async Task UpdateParser(Parser obj)
        {
            try
            {
                foreach (var u in obj.Newslist)
                {
                    if ((TimeChron.GetRealTime() - new DateTime(long.Parse(u.CachedOnStr)))
                        .Hours > App.Get.Config.CacheAlive)
                    {
                        await NewsItemDetailed.Process(u);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}