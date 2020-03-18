using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MaxRev.Servers.Interfaces;
using MaxRev.Servers.Utils.Logging;
using MaxRev.Utils.FileSystem;
using Newtonsoft.Json;
using NUWM.Servers.Core.Calc.Models;
using NUWM.Servers.Core.Calc.Services.Parsers;

namespace NUWM.Servers.Core.Calc.Services
{
    public class CacheHelper
    {
        private const string CacheFileName = "cached_all.json";
        private readonly DirectoryManager<App.Directories> _dm;
        private readonly ILogger _logger;
        private readonly SpecialtyParser _parser;
        public CacheHelper(DirectoryManager<App.Directories> dm, ILogger logger, SpecialtyParser parser)
        {
            _dm = dm;
            _logger = logger;
            _parser = parser;
            parser.OnCacheRequired += async () => await LoadCache();
            parser.OnParsed += async () => await SaveCache();
        }

        public async Task LoadCache()
        {
            try
            {
                var file = Path.Combine(_dm[App.Directories.Cache], CacheFileName);
                if (File.Exists(file))
                {
                    using var t = File.OpenText(file);
                    _parser.LoadSpecialtyList(JsonConvert.DeserializeObject<List<SpecialtyInfo>>(await t.ReadToEndAsync()));
                }
            }
            catch (Exception ex) { _logger.NotifyError(LogArea.Other, ex); }
        }
        public async Task SaveCache()
        {
            try
            {
                var f = Path.Combine(_dm[App.Directories.Cache], CacheFileName);
                if (_parser != null && _parser.SpecialtyList.Count > 0)
                {
                    await File.WriteAllTextAsync(f, JsonConvert.SerializeObject(_parser.SpecialtyList));
                }
            }
            catch (Exception ex) { _logger.NotifyError(LogArea.Other, ex); }
        }
    }
}