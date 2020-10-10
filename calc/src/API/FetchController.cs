using MaxRev.Servers.API.Controllers;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NUWM.Servers.Core.Calc.Models;
using NUWM.Servers.Core.Calc.Services;

namespace NUWM.Servers.Core.Calc.API
{
    [RouteBase("api/fetch")]
    public class FetchController : CoreApi
    {
        protected override void OnInitialized()
        {
            ModuleContext.StreamContext.KeepAlive = false;
        }
        [Route("{id}")]
        public IResponseInfo Info1(string id)
        {
            var service = Services.GetRequiredService<FetchService>();
            var result = service.GetById(id);
            return Builder.Content(new Response
            {
                Code = result != default ? StatusCode.Success : StatusCode.ServerSideError,
                Content = result
            }).Build();
        }
    }
}