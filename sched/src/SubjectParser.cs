using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MaxRev.Servers.Utils.Filesystem;

namespace NUWM.Servers.Core.Sched
{
    public sealed class SubjectParser
    {
        private string[] sr;

        public enum PType
        {
            Stud,
            Lect
        }

        private Dictionary<PType, string> patern;

        private List<SubjectInstance> list;
        private Exception err;
        public Dictionary<string, string> AR;
        public static SubjectParser Current;

        public SubjectParser()
        {
            err = null;
            patern = null;
            sr = null;
            list = new List<SubjectInstance>();
            UpdatePatern().GetAwaiter().GetResult();
            UpdateAutoreplace();
            Current = this;
        }

        public Tuple<DayInstance, Exception> Parse(string dateStr, string lines, bool isLecturer)
        {
            err = null;
            var rlist = new List<SubjectInstance>();
            try
            {
                if (patern == null || patern.Count == 0)
                {
                    throw new Exception("PATTERNS ARE EMPTY");
                }

                sr = lines.Split('\n');
                list = new List<SubjectInstance>();
                foreach (var i in sr)
                {
                    var time = "";
                    if (i.Substring(0, i.IndexOf(' ') + 1).Contains('-'))
                    {
                        time = i.Substring(0, i.IndexOf(' '));
                    }

                    var trash = i.Substring(i.IndexOf(' ') + 1);
                    rlist.AddRange(Parsing(time, trash.Split('\r', '\n'), isLecturer));
                }

                var date = DateTime.ParseExact(dateStr, "dd.MM.yyyy", null);
                return new Tuple<DayInstance, Exception>(new DayInstance(dateStr, date.DayOfWeek.ToString())
                {
                    Subjects = rlist.ToArray()
                }, err);
            }
            catch (Exception)
            {
                return new Tuple<DayInstance, Exception>(null, err);
            }
        }

        public SubjectInstance[] Parsing(string time, string[] subjectS, bool isLecturer)
        {
            if (patern == null || patern.Count == 0)
            {
                throw new InvalidOperationException("Emergency!!! PATTERNS ARE EMPTY");
            }

            var tm = ScheduleTimeViewItem.GetNum(time);
            list = new List<SubjectInstance>();
            var r = new Regex(patern[isLecturer ? PType.Lect : PType.Stud], RegexOptions.ECMAScript, TimeSpan.FromSeconds(5));
            if (!isLecturer) subjectS = subjectS.Select(x => ' ' + x).ToArray();
            foreach (var sub in subjectS.Where(x => x.Length > 2))
            {
                var mc = r.Matches(sub.Replace('\r', ' '));

                var subMatchAny = false;
                foreach (Match m in mc)
                {
                    if (m.Length < 2)
                    {
                        continue;
                    }

                    subMatchAny = true;
                    SubjectInstance _subject;
                    var g = m.Groups;
                    if (isLecturer)
                    {
                        var sname = g[5].Value.Trim();
                        sname = string.IsNullOrEmpty(sname) ? g[4].Value : sname;
                        _subject = new SubjectInstance(tm.LessonTime, sname, tm.LessonNum.ToString())
                        {
                            Classroom = g[1].Value.Trim(),
                            Type = CheckType(g[6].Value).Trim(),
                            Streams = g[2].Value.Trim(),
                            SubGroup = g[3].Value.Trim()
                        };
                    }
                    else
                    {
                        var subgroup = string.IsNullOrEmpty(g[5].Value) ? g[3].Value : g[5].Value;
                        subgroup = subgroup.TrimStart(' ', '(').TrimEnd(' ', ')');
                        if (string.IsNullOrEmpty(subgroup))
                        {
                            subgroup = "вся група";
                        }

                        var subj = !string.IsNullOrEmpty(g[6].Value.Trim()) ? g[6].Value.Trim() : g[0].Value.Trim();
                        _subject = new SubjectInstance(tm.LessonTime, subj, tm.LessonNum.ToString())
                        {
                            SubGroup = subgroup,
                            Streams = g[4].Value.Trim(),
                            Lecturer = g[2].Value.Trim(),
                            Classroom = g[1].Value.Trim(),
                            Type = CheckType(g[7].Value).Trim()
                        };

                    }

                    list.Add(_subject);
                }

                if (!subMatchAny)
                {
                    var g = new Regex(@"^(.*).*(?:-)\s*(.*[^\s])", RegexOptions.ECMAScript).Match(
                        sub.Trim('-', ' '));
                    if (g.Length > 2)
                        list.Add(new SubjectInstance(tm.LessonTime, g.Groups[2].Value, tm.LessonNum.ToString())
                        {
                            Classroom = g.Groups[1].Value
                        });
                    else list.Add(new SubjectInstance(tm.LessonTime, sub.Trim('-', ' '), tm.LessonNum.ToString()));
                }

            }


            return list.ToArray();
        }

        private string AutoReplace(string pat)
        {
            var cg = pat;
            try
            {
                if (pat.Count(x => x == ' ') < 2 && pat.Length < 20)
                {
                    return pat;
                }

                foreach (var k in AR.Keys)
                {
                    if (pat.ToLower().EndsWith(k.ToLower()))
                    {
                        cg = pat.Replace(k, AR[k]);
                    }
                }

                var obj = AutoReplaceHelper.SmartSearch(pat);
                if (obj.Any())
                {
                    cg = obj.First();
                }
#if DEBUG
                Console.WriteLine("Replaced: {0} => {1}", pat, cg);
#endif
            }
            catch
            {
                // ignored
            }

            return cg;
        }

        public bool UpdateAutoreplace()
        {
            var f = "./addons/subjects_parser/autoreplace.txt";
            if (File.Exists(f))
            {
                AR = new Dictionary<string, string>();
                var g = File.OpenText(f);
                var h = g.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                var r = new Regex(@"(?m)^(.*)\s-\s(.*)", RegexOptions.ECMAScript);
                foreach (var s in h)
                {
                    var m = r.Match(s).Groups;
                    AR.Add(m[1].Value, m[2].Value);
                }

                return true;
            }

            return false;
        }

        private async Task<bool> GetPatternFor(PType type)
        {
            var folder =
                MainApp.Current.Core.DirectoryManager.GetFor<MainApp.Dirs>(Dirs.WorkDir)[MainApp.Dirs.Subject_Parser];
            //MainApp.Current.Core.DirectoryManager[MainApp.Dirs.Subject_Parser];
            var fmt = "ddMMyy";
            string[] files;
            while (true)
            {
                files = Directory.EnumerateFiles(folder).ToArray();
                if (files.Length != 0)
                    break;
                Console.WriteLine("Awaiting patterns");
                await Task.Delay(5000);
            }

            var lastDate = new DateTime();
            foreach (var file in files)
            {
                var f = new FileInfo(file);
                if (f.Name.Contains(type.ToString().ToLower()))
                {
                    var pts = f.Name.Split('_');
                    if (DateTime.TryParseExact(pts[1], fmt, null, DateTimeStyles.None, out var date)
                        && lastDate < date)
                    {
                        lastDate = date;
                    }
                }
            }

            var may = Path.Combine(folder,
                $"pattern_{lastDate.ToString(fmt)}{'_'}{type.ToString().ToLower()}.txt");
            if (File.Exists(may))
            {
                patern.Add(type, File.ReadAllText(may));
                return true;
            }

            return false;
        }

        public async Task<bool> UpdatePatern()
        {
            patern = new Dictionary<PType, string>();
            var result = new List<bool>();
            foreach (var a in Enum.GetValues(typeof(PType)).Cast<PType>().ToList())
            {
                result.Add(await GetPatternFor(a));
            }

            return result.All(x => x);
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        private static string CheckType(string what)
        {
            what = what.Replace('(', ' ')
                .Replace(')', ' ')
                .Trim();
            return what switch
            {
                "Л" => "Лекція",
                "Лаб" => "Лабораторна робота",
                "ПрС" => "Практичне заняття",
                "Екз" => "Екзамен",
                "Зал" => "Залік",
                "ТЕСТ" => "Тест",
                "Конс" => "Консультація",
                "Реф" => "Реферат",
                "ІнЗн" => "Індивідуальне заняття",
                "Сем" => "Семінар",
                "Сам" => "Самостійна робота",
                "МК" => "Модульний контроль",
                "КсКР" => what,
                _ => what
            };
        }

        /// <summary>
        /// Schedule view presentation of date for displaying in apps 
        /// </summary>
        public class ScheduleTimeViewItem
        {
            static ScheduleTimeViewItem()
            {
                _romanMap = new Dictionary<char, int>
                  {
                      {'I', 1}, {'V', 5}, {'X', 10}, {'L', 50}, {'C', 100}, {'D', 500}, {'M', 1000}
                  };
                ViewItems = new[]{
                    new ScheduleTimeViewItem("08:00-09:20/20", "I"),
                    new ScheduleTimeViewItem("09:40-11:00/15", "II"),
                    new ScheduleTimeViewItem("11:15-12:35/25", "III"),
                    new ScheduleTimeViewItem("13:00-14:20/15", "IV"),
                    new ScheduleTimeViewItem("14:35-15:55/10", "V"),
                    new ScheduleTimeViewItem("16:05-17:25/10", "VI"),
                    new ScheduleTimeViewItem("17:35-18:55/10", "VII"),
                    new ScheduleTimeViewItem("19:05-20:25/0", "VIII")
                };
            }

            private static readonly ScheduleTimeViewItem[] ViewItems;

            /// <summary>
            /// Delimiter is - and /  so format is "hh:mm-hh:mm/mm" 
            /// </summary>
            public ScheduleTimeViewItem(string time, string num)
            {
                var tm = time.Split('/');
                LessonTime = tm[0];
                LessonNumRom = num;
                LessonNum = ConvertRomanToNumber(num);
                if (tm[1] != "0")
                {
                    AfterBreak = string.Format("{0} {1}", tm[1], "minutes");
                }
            }

            public int LessonNum { get; set; }
            public string LessonNumRom { get; set; }
            public string LessonTime { get; set; }
            public string AfterBreak { get; set; }

            public static ScheduleTimeViewItem GetNum(string time)
            {
                if (ViewItems == null || time == "")
                {
                    return null;
                }

                return ViewItems.FirstOrDefault(x => x.LessonTime.Contains(time.Substring(1, 8)));
            }

            public static ScheduleTimeViewItem GetTime(string num)
            {
                return ViewItems.FirstOrDefault(x => x.LessonNum == int.Parse(num));
            }

            private static readonly Dictionary<char, int> _romanMap;

            private static int ConvertRomanToNumber(string text)
            {
                int totalValue = 0, prevValue = 0;
                foreach (var c in text)
                {
                    if (!_romanMap.ContainsKey(c))
                    {
                        return 0;
                    }

                    var crtValue = _romanMap[c];
                    totalValue += crtValue;
                    if (prevValue != 0 && prevValue < crtValue)
                    {
                        if (prevValue == 1 && (crtValue == 5 || crtValue == 10)
                            || prevValue == 10 && (crtValue == 50 || crtValue == 100)
                            || prevValue == 100 && (crtValue == 500 || crtValue == 1000))
                        {
                            totalValue -= 2 * prevValue;
                        }
                        else
                        {
                            return 0;
                        }
                    }

                    prevValue = crtValue;
                }

                return totalValue;
            }

        }

    }

    // Not relevant now - already fixed on server
}