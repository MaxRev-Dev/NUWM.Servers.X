using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaxRev.Servers.Interfaces;
using NUWEE.Servers.Core.News.Json;

namespace NUWEE.Servers.Core.News.Parsers
{
    [Serializable]
    public abstract class AbstractParser : IDisposable
    {
        protected readonly ILogger _logger;

        internal string Url { get; private set; }
        public string Key { get; private set; }
        public int InstituteID { get; private set; }

        protected List<NewsItem> _newsList = new List<NewsItem>();
        public int CacheEpoch { get; set; }

        public AbstractParser(ILogger logger)
        {
            _logger = logger;
        }
        public AbstractParser FromParams(string url, string key, int institute_id = -100)
        {
            Key = key;
            InstituteID = institute_id;
            Url = url;
            return this;
        }


        public abstract Task ParsePagesAsync(string parser_url);
        public List<NewsItem> Newslist
        {
            get => _newsList ?? (_newsList = new List<NewsItem>());
            set {
                if (value != null)
                {
                    _newsList.Clear();
                    _newsList = value;
                }
            }
        }

        public void Dispose()
        {
            _newsList.Clear();
        }
    }
}