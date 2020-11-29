using System;
using Microsoft.Extensions.DependencyInjection;

namespace NUWEE.Servers.Core.News.Parsers
{
    public class ParserFactory
    {
        public ParserFactory(IServiceProvider services)
        {
            _services = services;
        }
        private readonly IServiceProvider _services;
        public AbstractParser GetParser(string url, string key, bool abitParser, int institute_id = -100)
        {
            var parser = abitParser
                ? (AbstractParser)_services.GetRequiredService<AbitNewsParser>()
                : _services.GetRequiredService<NewsParser>();
            return parser.FromParams(url, key, institute_id);
        }
    }
}