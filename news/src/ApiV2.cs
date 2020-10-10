using System;
using System.Collections.Generic;
using JSON;
using Lead;
using MaxRev.Servers.Core.Route;

namespace APIUtilty
{
    [RouteBase("api2")]
    internal class ApiV2 : API
    {
        public override Response CreateResponse(List<NewsItem> obj, Exception err, InstantCacher.InstantState state = InstantCacher.InstantState.Success)
        {
            return new ResponseV2(base.CreateResponse(obj, err, state));
        } 
    }
}