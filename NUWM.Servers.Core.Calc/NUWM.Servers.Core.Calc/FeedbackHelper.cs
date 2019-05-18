using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaxRev.Utils;
using MaxRev.Utils.Schedulers;

namespace NUWM.Servers.Core.Calc
{
    internal class FeedbackHelper : BaseScheduler
    {
        public FeedbackHelper() : base(TimeSpan.FromHours(6))
        {
            Feed = new Dictionary<string, string>();
            CurrentWorkHandler = () =>
            {
                if (Feed.Any())
                {
                    Save();
                    Feed.Clear();
                }
            };
        }
        public readonly Dictionary<string, string> Feed;
        public void Save()
        {
            var st = TimeChron.GetRealTime().ToLongDateString();
            File.AppendAllText(Path.Combine(App.Get.Core.DirectoryManager[App.Dirs.Feedback], "users_reviews" + st + ".txt"), GetAll(true));
            for (int i = 0; i < Feed.Count; i++)
                Feed[Feed.ElementAt(i).Key] = "";
        }

        public string GetAll(bool strict = false)
        {
            string all = "";

            var f = strict ? Feed.Where(x => x.Value != "") : Feed;
            foreach (var i in f)
                all += i.Key + "\n" + i.Value + "\n\n";
            return all;
        }

        public bool Checker(string key)
        {
            var g = Feed.Where(x => x.Key != null && x.Key.Contains(key)).ToArray();
            if (g.Any())
            {
                if (TimeChron.GetRealTime() - DateTime.ParseExact(g.Last().Key.Split("=>")[1].Trim(' '), "hh:mm:ss - dd.MM.yyyy", null) > new TimeSpan(0, 5, 0))
                    return true;
                return false;
            }
            return true;
        }
    }
}