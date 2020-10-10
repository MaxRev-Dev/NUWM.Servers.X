using System;
using MaxRev.Servers.Utils;
using MaxRev.Utils.Schedulers;
using Microsoft.Extensions.DependencyInjection;
using NUWM.Servers.Core.News;

namespace Lead
{
    [Serializable]
    public class PoolParserScheduler : BaseScheduler
    {
        private readonly IServiceProvider _services;
        private ParserPool _parserPool => _services.GetRequiredService<ParserPool>();
        private readonly CacheManager _cacheManager;

        public PoolParserScheduler(IServiceProvider services, CacheManager cacheManager)
        {
            _services = services;
            _cacheManager = cacheManager;
            CurrentWorkHandler = ReparseTask;
        }

        private string ParserKey { get; set; }
        private TimeSpan DefTime { get; set; }
        private TimeSpan EmergTime { get; } = new TimeSpan(0, 0, 5);
        public async void ReparseTask()
        {
            if (Tools.CheckForInternetConnection())
            {
                var oldOne = _parserPool[ParserKey];
                SetDelay(DefTime);
                await _cacheManager.SaverAsync(ParserKey).ConfigureAwait(false);
                var factory = _services.GetRequiredService<ParserFactory>();
                var parser = factory.GetParser(oldOne.Url, ParserKey, oldOne is AbitNewsParser, oldOne.InstituteID);
                parser.CacheEpoch = ++oldOne.CacheEpoch;
                await parser.ParsePagesAsync(parser.Url).ConfigureAwait(false);
                _parserPool[ParserKey] = parser;
                oldOne?.Dispose();
            }
            else
            {
                SetDelay(EmergTime);
            }

            ScheduleTimer();

        }

        public PoolParserScheduler WithParameters(Parser parser, TimeSpan after)
        {
            int delayMins = 0, delayHours = 0;

            if (parser.InstituteID != -100)
            {
                delayHours = MainApp.Config.ReparseTaskDelayHours;
            }
            else
            {
                delayMins = MainApp.Config.ReparseTaskDelayMinutes;
            }
            ParserKey = parser.Key;

            SetDelay(DefTime.Add(after));
            DefTime = new TimeSpan(delayHours, delayMins, 0);
            return this;
        }
    }
}