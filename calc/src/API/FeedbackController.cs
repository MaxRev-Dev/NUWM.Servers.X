using MaxRev.Servers.API.Controllers;
using MaxRev.Servers.Core.Route;
using MaxRev.Servers.Interfaces;
using MaxRev.Utils;
using MaxRev.Utils.Methods;
using Microsoft.Extensions.DependencyInjection;
using NUWM.Servers.Core.Calc.Models;
using NUWM.Servers.Core.Calc.Services;

namespace NUWM.Servers.Core.Calc.API
{
    [RouteBase("api/feedback")]
    public class FeedbackController : CoreApi
    { 
        [Route("", AccessMethod.POST)]
        public string FeedbackPost()
        {
            var gu = StatusCode.Success;
            string cont;
            if (FeedbackHandler(Info.FormData))
                cont = "Дякуємо за Ваш відгук!";
            else
            {
                cont = "Ваш відгук не зараховано. Перевищено кількість запитів. Повторіть спробу за декілька хвилин";
                gu = StatusCode.ServerSideError;
            }
            return new Response { Content = cont, Code = gu }.Serialize();
        }

        private bool FeedbackHandler(IRequestData Content)
        {
            var feedbackHelper = Services.GetRequiredService<FeedbackHelper>();
            try
            {
                var qur = Content.Form;
                if (!qur.TryGetValue("mail", out var c1) ||
                    !feedbackHelper.Checker(c1))
                {
                    return false;
                }

                qur.TryGetValue("text", out var c2);
                feedbackHelper.AddAndSave(c1 + " => " + TimeChron.GetRealTime().ToString("hh:mm:ss - dd.MM.yyyy"), c2);
            }
            catch { return false; }
            return true;
        }
    }
}