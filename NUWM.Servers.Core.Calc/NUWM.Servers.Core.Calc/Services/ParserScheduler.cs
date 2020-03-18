using MaxRev.Servers.Utils;
using MaxRev.Utils.Schedulers;
using NUWM.Servers.Core.Calc.Config;
using NUWM.Servers.Core.Calc.Services.Parsers;

namespace NUWM.Servers.Core.Calc.Services
{
    public class ParserScheduler : BaseScheduler
    {
        private SpecialtyParser Parser { get; }

        public ParserScheduler(SpecialtyParser parser, CalcConfig config) : base(config.UpdateDelay)
        {
            Parser = parser;
            CurrentWorkHandler = async () =>
            {
                if (Tools.CheckForInternetConnection())
                {
                    await Parser.ReloadTables();
                }
                SetDelay(config.UpdateDelay);
            };
        }
    }
}