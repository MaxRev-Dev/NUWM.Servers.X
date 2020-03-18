using System;
using System.Collections.Generic;
using HtmlAgilityPack;
using MaxRev.Utils.Schedulers;
using NUWM.Servers.Core.Calc.Config;

namespace NUWM.Servers.Core.Calc.Services
{
    public class FetchService : BaseScheduler
    {
        private readonly CalcConfig _config;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

        public FetchService(CalcConfig config)
        {
            _config = config;
            SetDelay(_config.UpdateDelay);
        }

        protected override void OnTimerElapsed()
        {
            foreach (var key in _cache.Keys)
            {
                try
                {
                    Fetch(key);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public string GetById(string id)
        {
            if (_config.FetchMap.ContainsKey(id))
            {
                if (_cache.ContainsKey(id))
                    return _cache[id];
            }
            return Fetch(id);
        }

        private string Fetch(string id)
        {
            var web = new HtmlWeb();
            var endpoint = _config.FetchMap[id];
            var doc = web.Load(endpoint.Url);
            var html = doc.DocumentNode.SelectSingleNode(endpoint.XPath).OuterHtml;
            var uri = new Uri(endpoint.Url);
            Preprocess(uri.Host, ref html);
            return AddToCache(id, html);
        }

        private void Preprocess(string host, ref string html)
        {
            html = html.Replace("src=\"/", $"src=\"http://{host}/");
            html = html.Replace("href=\"/", $"href=\"http://{host}/");
            html = html.Replace("src=\"./", $"src=\"http://{host}/");
            html = html.Replace("href=\"./", $"href=\"http://{host}/");
            html = html.Replace("&nbsp;", "");
        }

        private string AddToCache(string id, string html)
        {
            _cache[id] = html;
            return html;
        }
    }
}