using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaxRev.Utils;
using MaxRev.Utils.FileSystem;
using MaxRev.Utils.Schedulers;
using Microsoft.Extensions.Primitives;

namespace NUWM.Servers.Core.Calc.Services
{
    public class FeedbackHelper : BaseScheduler
    {
        private readonly DirectoryManager<App.Directories> _directoryManager;
        private readonly object _gate = new object();
        public FeedbackHelper(DirectoryManager<App.Directories> directoryManager) : base(TimeSpan.FromHours(6))
        {
            _directoryManager = directoryManager;
            Feed = new Dictionary<string, string>();
            CurrentWorkHandler = () =>
            {
                lock (_gate)
                    if (Feed.Any())
                    {
                        Save();
                        Feed.Clear();
                        _feedCleanupTime = DateTimeOffset.Now;
                    }
            };
        }

        private DateTimeOffset _feedCleanupTime;

        private Dictionary<string, string> Feed { get; }

        public void Save()
        {
            lock (_gate)
            {
                var st = TimeChron.GetRealTime().ToLongDateString();
                File.AppendAllText(
                    Path.Combine(_directoryManager[App.Directories.Feedback],
                        "users_reviews" + st + ".txt"), GetAll(true));
                for (int i = 0; i < Feed.Count; i++)
                    Feed[Feed.ElementAt(i).Key] = "";
            }
        }

        public string GetAll(bool strict = false)
        {
            lock (_gate)
            {
                string all = "";

                var f = strict ? Feed.Where(x => x.Value != "") : Feed;
                foreach (var i in f)
                    all += i.Key + "\n" + i.Value + "\n\n";
                return all;
            }
        }

        public bool Checker(string key)
        {
            lock (_gate)
            {
                // if cleanup was just in last 5 minutes
                if (DateTimeOffset.Now - _feedCleanupTime < TimeSpan.FromMinutes(5))
                {
                    return true;
                }

                // search for record with key in current feed
                var g = Feed.Where(x => x.Key != null && x.Key.Contains(key)).ToArray();
                if (g.Any())
                {
                    // allow feedback if last review was more than 5 minutes ago
                    return TimeChron.GetRealTime() - DateTime.ParseExact(g.Last().Key.Split("=>")[1].Trim(' '),
                               "hh:mm:ss - dd.MM.yyyy", null) > new TimeSpan(0, 5, 0);
                }

                return true;
            }
        }

        public void AddAndSave(string key, in StringValues value)
        {
            Feed.Add(key, value);
            Save();
        }
    }
}