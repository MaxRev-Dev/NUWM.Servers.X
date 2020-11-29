using System;
using System.Threading.Tasks;
using MaxRev.Utils;
using MaxRev.Utils.Schedulers;
using NUWEE.Servers.Core.News.Parsers;

namespace NUWEE.Servers.Core.News.Updaters
{
    [Serializable]
    public class CacheUpdater : BaseScheduler
    {
        private readonly ParserPool _parserPool;

        public CacheUpdater(ParserPool parserPool)
        {
            _parserPool = parserPool;
            CurrentWorkHandler = CheckForUpdates;
            SetDelay(new TimeSpan(1, 0, 0));
            ScheduleTimer();
        }
        public void CheckForUpdates()
        {
            foreach (var i in _parserPool.Values)
            {
                Task.Run(() => UpdateParser(i));
            }
        }
        private static async void UpdateParser(AbstractParser obj)
        {
            try
            {
                foreach (var u in obj.Newslist)
                {
                    if ((TimeChron.GetRealTime() - new DateTime(long.Parse(u.CachedOnStr)))
                        .Hours > MainApp.Config.CacheAliveHours)
                    {
                        await u.FetchAsync().ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}