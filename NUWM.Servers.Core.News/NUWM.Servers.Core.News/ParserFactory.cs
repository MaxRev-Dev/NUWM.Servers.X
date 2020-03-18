using System;
using Microsoft.Extensions.DependencyInjection;

namespace Lead
{
    public class ParserFactory
    {
        public ParserFactory(IServiceProvider services)
        {
            _services = services;
        }
        private readonly IServiceProvider _services;
        public Parser GetParser(string url, string key, bool abitParser, int institute_id = -100)
        {
            var parser = abitParser
                ? (Parser)_services.GetRequiredService<AbitNewsParser>()
                : _services.GetRequiredService<NewsParser>();
            return parser.FromParams(url, key, institute_id);
        }
    }
}