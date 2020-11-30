using System;
using System.Collections.Generic;
using MaxRev.Servers.Core.Route;
using NUWEE.Servers.Core.News.Json;
using NUWEE.Servers.Core.News.Updaters;

namespace NUWEE.Servers.Core.News.API
{
    [RouteBase("news/api2")]
    internal class ApiV2 : API
    {
        public override Response CreateResponse(List<NewsItem> obj, Exception err, InstantCacher.InstantState state = InstantCacher.InstantState.Success)
        {
            return new ResponseV2(base.CreateResponse(obj, err, state));
        } 
    }
}