using HierarchyTime;
using HtmlAgilityPack;
using MR.Servers;
using MR.Servers.Utils;
using Newtonsoft.Json;
using NUWM.Servers.Sched;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static SubjectParser.ScheduleTimeViewItem;

namespace DataSpace
{
    /// <summary>
    /// Almost big kitchen.
    /// Manages requests to desk. Uses html parsers and schedule parser.
    /// and returns final representation of weeks and days hierarchy
    /// </summary>
    public sealed class GetData
    {
        #region Vars
        private HttpResponseMessage responseMessage;
        private readonly string sname, lecturer;
        public string StartDate { get; private set; }
        public string EndDate { get; private set; }
        public bool TimetableForLecturer { get; private set; }

        private List<WeekInstance> CurrentDays;
        private List<DayInstance> CurrentParsed;

        private CultureInfo cultureInfo = new CultureInfo("uk-UA");
        private DateTimeFormatInfo dateTimeInfo;
        public Exception R = null;
        private readonly RetType ReturnType;
        public enum RetType
        {
            weeks, days
        }
        #endregion
        /// <summary>
        /// Start point. First type of requests and it defines straight value settings
        /// </summary>
        /// <param name="name">Group or lecturer name</param>
        /// <param name="sdate">Start date [dd.MM.yyyy]</param>
        /// <param name="edate">End date [dd.MM.yyyy]</param>
        /// <param name="isLecturer">Timetable is for lecturer</param>
        /// <param name="retType">what to return: weeks or days</param>
        public GetData(string name, string sdate, string edate, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            if (isLecturer)
            {
                lecturer = name;
            }
            else
            {
                sname = name;
            }

            StartDate = sdate;
            EndDate = edate;
            TimetableForLecturer = isLecturer;
            dateTimeInfo = cultureInfo.DateTimeFormat;
        }
        /// <summary>
        ///  Start point. Simplified request similar to web-schedule
        /// </summary>
        /// <param name="name">Group or lecturer name</param>
        /// <param name="week">number of week [1-52]</param>
        /// <param name="year">year [YYYY]</param>
        /// <param name="isLecturer">Timetable is for lecturer</param>
        /// <param name="retType">what to return: weeks or days</param>
        public GetData(string name, int week, int year, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            if (isLecturer)
            {
                lecturer = name;
            }
            else
            {
                sname = name;
            }

            SetWeek(week, year);
            TimetableForLecturer = isLecturer;
            dateTimeInfo = cultureInfo.DateTimeFormat;
        }
        /// <summary>
        ///  Start point. Simplified request similar to web-schedule. For specifying weeks range 
        /// </summary>
        /// <param name="name">Group or lecturer name</param>
        /// <param name="weekfst">First week. [1-52] Start of schedule</param>
        /// <param name="weeklst">Last week. [1-52] End of schedule</param>
        /// <param name="year">Year [YYYY]</param>
        /// <param name="isLecturer">Timetable is for lecturer</param>
        /// <param name="retType">what to return: weeks or days<</param>
        public GetData(string name, int weekfst, int weeklst, int year, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            if (isLecturer)
            {
                lecturer = name;
            }
            else
            {
                sname = name;
            }

            SetWeek(weekfst, weeklst, year);
            TimetableForLecturer = isLecturer;
            dateTimeInfo = cultureInfo.DateTimeFormat;
        }

        /// <summary>
        /// Sets weeks range for datetime values in current instance. Helper for constructor
        /// </summary> 
        private void SetWeek(int week1, int week2, int year)
        {
            week1--;
            week2--;
            int curweek = 0;
            DateTime dateTime = new DateTime(year, 1, 1);
            while (curweek != week1)
            {
                curweek = WeekInstance.GetIso8601WeekOfYear(dateTime);
                dateTime = dateTime.AddDays(7);
            }
            StartDate = WeekInstance.StartOfWeek(dateTime, DayOfWeek.Monday).ToString("dd.MM.yyyy");
            curweek = 0;
            dateTime = new DateTime((week1 < week2) ? year : year + 1, 1, 1);
            while (curweek != week2)
            {
                curweek = WeekInstance.GetIso8601WeekOfYear(dateTime);
                dateTime = dateTime.AddDays(7);
            }
            EndDate = WeekInstance.StartOfWeek(dateTime, DayOfWeek.Monday).AddDays(6).ToString("dd.MM.yyyy");
        }

        /// <summary>
        /// Sets weeks for datetime value in current instance. Helper for constructor
        /// </summary>
        /// <param name="week"></param>
        /// <param name="year"></param>
        private void SetWeek(int week, int year)
        {
            week--;
            int curweek = 0;
            DateTime dateTime = new DateTime(year, 1, 1);
            while (curweek != week)
            {
                curweek = WeekInstance.GetIso8601WeekOfYear(dateTime);
                dateTime = dateTime.AddDays(7);
            }
            StartDate = WeekInstance.StartOfWeek(dateTime, DayOfWeek.Monday).ToString("dd.MM.yyyy");
            EndDate = WeekInstance.StartOfWeek(dateTime, DayOfWeek.Monday).AddDays(6).ToString("dd.MM.yyyy");
        }
        /// <summary>
        /// Retrieves schedule from server and parses it. Handles all errors
        /// </summary>
        /// <param name="auto"></param>
        /// <returns></returns>
        public async Task<object> GetDays(bool auto)
        {
            Uri requestUri = new Uri("http://desk.nuwm.edu.ua/cgi-bin/timetable.cgi");
            try
            {
                try
                {
                    CreateClientRequest request = new CreateClientRequest(requestUri.OriginalString);
                    responseMessage = await request.PostAsync(JsonData());
                }
                catch { throw new OperationCanceledException("Connection failed"); }
                responseMessage.EnsureSuccessStatusCode();
                string data = await ParseResponse();
                if (!data.Contains("не знайдено") && !data.Contains("У програмі виникла помилка"))
                {
                    ParseHtmlToData(data, auto);

                    if (ReturnType == RetType.weeks)
                    {
                        return CurrentDays;
                    }

                    return CurrentParsed;
                }
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(data);
                if (data.Contains("не знайдено"))
                {
                    R = new InvalidDataException("Not Found");
                }
                else
                {
                    R = new Exception(doc.DocumentNode.Descendants().Where(x => x.HasClass("alert")).First().InnerText);
                }
            }
            catch (OperationCanceledException ex) { throw ex; }
            catch (NullReferenceException ex) { throw ex; }
            catch (Exception ex) { R = ex; }
            if (ReturnType == RetType.weeks)
            {
                return new List<WeekInstance>();
            }

            return new List<DayInstance>();
        }

        /// <summary>
        /// Searching schedule table in html 
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        private HtmlNode FindTable(HtmlDocument doc)
        {
            HtmlNode dsd = doc.CreateElement("div");
            List<HtmlNode> y = doc.DocumentNode.Descendants().Where
                  (x => (x.Name == "div" && x.HasClass("jumbotron"))).ToList();
            if (y.Any())
            {
                HtmlNode footer = y.First().Descendants().Where(x => x.HasClass("container")).First();
                foreach (HtmlNode r in footer.Elements("div"))
                {
                    dsd.AppendChild(r);
                }

                return dsd;
            }
            return null;
        }
        /// <summary>
        /// Parses html into sublect objects 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="auto"></param>
        public void ParseHtmlToData(string data, bool auto)
        {
            data = data.Insert(data.IndexOf("</head>"), "<base href=\"#!\">");
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(data);
            CurrentDays = new List<WeekInstance>();
            HtmlNode tableWrapper = FindTable(doc);

            if (tableWrapper == null)
            {
                return; // No schedule
            }

            CurrentParsed = new List<DayInstance>();
            foreach (HtmlNode iy in tableWrapper.Descendants("div").Where(x => x.HasClass("col-md-6")))
            {
                CurrentParsed.Add(ParseDay(iy, auto));

                // Fixing lost subjects
                if (iy.NextSibling != null && iy.NextSibling.HasClass("row"))
                {
                    HtmlNode t = iy.NextSibling;
                    while (t != null && t.HasClass("row"))
                    {
                        List<SubjectInstance> tgt = CurrentParsed.Last().Subjects.ToList();
                        tgt.AddRange(ParseDayFix(t, auto));
                        CurrentParsed.Last().Subjects = tgt.ToArray();
                        t = t.NextSibling;
                    }
                }
            }

            if (ReturnType == RetType.weeks)
            {
                CurrentDays = new List<WeekInstance>(
                    new[] { new WeekInstance(CurrentParsed[0])
                    });
                for (int it = 1; it < CurrentParsed.Count; it++)
                {
                    DayInstance next = CurrentParsed[it];
                    IEnumerable<WeekInstance> inf = CurrentDays.Where(x => x.Contains(next));
                    if (inf.Count() == 1)
                    {
                        inf.First().day.Add(next);
                    }
                    else
                    {
                        CurrentDays.Add(new WeekInstance(CurrentParsed[it]));
                    }
                }
                CurrentParsed.Clear();
            }

        }
        /// <summary>
        /// Parses day cell in html table
        /// </summary>
        /// <param name="node">Node witch contains day </param>
        /// <param name="auto"></param>
        /// <returns></returns>
        private DayInstance ParseDay(HtmlNode node, bool auto)
        {
            string g = node.ChildNodes.FindFirst("h4").InnerText;
            DayInstance day = new DayInstance(

              //date && day
              date: node.ChildNodes.FindFirst("h4").ChildNodes[0].InnerText.Trim(' '),
               dayName: node.ChildNodes.FindFirst("h4").Element("small").InnerText
            );

            HtmlNode table = node.ChildNodes.FindFirst("table");
            List<SubjectInstance> slist = new List<SubjectInstance>();
            foreach (HtmlNode i in table.ChildNodes.Where(x => x.Name == "tr"))
            {
                IEnumerable<HtmlNode> t = i.Elements("td");
                string ind = t.ElementAt(0).InnerText;
                string times = t.ElementAt(1).InnerHtml.Replace("<br>", "-");
                string trashed = t.ElementAt(2).InnerHtml.Replace("<br>", "\r");
                slist.AddRange(InnerCheckParse(times, trashed, ind, auto));
            }
            day.Subjects = slist.ToArray();
            return day;
        }
        /// <summary>
        /// Html tags not closed properly so here's the FIX.
        /// Parsing lost subjects
        /// </summary>
        /// <param name="node"></param>
        /// <param name="auto"></param>
        /// <returns></returns>
        private SubjectInstance[] ParseDayFix(HtmlNode node, bool auto)
        {
            try
            {
                HtmlNode tr = node.Element("tr");
                string ind = tr.ChildNodes[0].InnerText;
                string times = tr.ChildNodes[1].InnerHtml.Replace("<br>", "-");
                string trashed = tr.ChildNodes[2].InnerHtml.Replace("<br>", "\r");
                return InnerCheckParse(times, trashed, ind, auto);
            }
            catch (Exception ex) { throw ex; }
        }
        /// <summary>
        /// Subject cell parse handling
        /// </summary>
        /// <param name="times">subject time</param>
        /// <param name="trashed">Trashed string with lecturer, subject and other goods </param>
        /// <param name="ind">Number of lesson</param>
        /// <param name="auto"></param>
        /// <returns></returns>
        private SubjectInstance[] InnerCheckParse(string times, string trashed, string ind, bool auto)
        {
            List<SubjectInstance> subj = new List<SubjectInstance>();
            if (trashed == "Фізичне виховання" || trashed == "Військова підготовка" || trashed == "Вчена рада" || trashed == "Директорат")
            {
                subj.Add(new SubjectInstance(times, trashed, ind));
            }
            else
            {
                if (trashed == "")
                {
                    return subj.ToArray(); /*subj.Add(new SubjectInstance(times, "", ind));*/
                }
                else
                {
                    SubjectInstance[] df = SubjectParser.SubjectParser.Current.Parsing
                        (times, trashed.Split(new char[] { '\r', '\n' }), TimetableForLecturer, auto, lecturer);
                    if (df != null)
                    {
                        subj.AddRange(df);
                    }
                }
            }
            return subj.ToArray();
        }

        #region FALLEN - Not actual now
        private async Task<List<WeekInstance>> GetCache()
        {
            string f = "./cache/sched/" + (TimetableForLecturer ? "lects" : "groups")
                   + "/" + (TimetableForLecturer ? lecturer : sname) + ".txt";
            if (File.Exists(f))
            {
                StreamReader t = File.OpenText(f);
                return JsonConvert.DeserializeObject<List<WeekInstance>>(await t.ReadToEndAsync());
            }
            return new List<WeekInstance>();
        }

        private async void SaveToCache()
        {
            CurrentDays = new List<WeekInstance>(
                new[] { new WeekInstance(CurrentParsed[0])
                });
            for (int it = 1; it < CurrentParsed.Count; it++)
            {
                DayInstance next = CurrentParsed[it];
                IEnumerable<WeekInstance> inf = CurrentDays.Where(x => x.Contains(next));
                if (inf.Count() == 1)
                {
                    inf.First().day.Add(next);
                }
                else
                {
                    CurrentDays.Add(new WeekInstance(CurrentParsed[it]));
                }
            }
            StreamWriter t = File.CreateText("./cache/sched/" + (TimetableForLecturer ? "lects" : "groups")
                + "/" + (TimetableForLecturer ? lecturer : sname) + ".txt");
            await t.WriteAsync(JsonConvert.SerializeObject(CurrentDays));
            t.Close();
        }
        #endregion
        /// <summary>
        /// Fills empty weeks that are not displayed in web-schedule
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private List<WeekInstance> FillEmpty(List<WeekInstance> list)
        {

            List<WeekInstance> retVal = new List<WeekInstance>();
            foreach (WeekInstance i in list)
            {
                WeekInstance week = new WeekInstance();

                foreach (DayInstance d in i.day)
                {
                    if (d.DayName == null || d.Day == null)
                    {
                        if (i.day.IndexOf(d) > 0)
                        {
                            DayInstance prev = i.day[i.day.IndexOf(d) - 1];
                            //var cday = DateTime.Parse(prev.Day).AddDays(1);
                            DateTime.TryParseExact(prev.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out DateTime tmp);
                            DateTime cday = tmp.AddDays(1);

                            d.Day = cday.ToString("dd.MM.yyyy");
                            d.DayName = FirstCharToUpper(dateTimeInfo.GetDayName(cday.DayOfWeek));
                        }
                        else
                        {
                            DayInstance next = i.day[i.day.IndexOf(d) + 1];
                            //var cday = DateTime.Parse(next.Day).AddDays(-1);
                            DateTime.TryParseExact(next.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out DateTime tmp);
                            DateTime cday = tmp.AddDays(1);

                            d.Day = cday.ToString("dd.MM.yyyy");
                            d.DayName = FirstCharToUpper(dateTimeInfo.GetDayName(cday.DayOfWeek));
                        }
                    }
                    week.day.Add(d);
                }
                retVal.Add(week);
            }
            return retVal;
        }
        /// <summary>
        /// Data preparations for posting to web-schedule
        /// </summary>
        /// <returns></returns>
        private HttpContent JsonData()
        {
            string encLecturer = "", encsname = "";
            if (!string.IsNullOrEmpty(sname))
            {
                byte[] Swin1251bytes = Encoding.GetEncoding("windows-1251").GetBytes(sname);
                string Shex = BitConverter.ToString(Swin1251bytes);
                encsname = "%" + Shex.Replace("-", "%");
            }
            if (!string.IsNullOrEmpty(lecturer))
            {
                byte[] Lwin1251bytes = Encoding.GetEncoding("windows-1251").GetBytes(lecturer);
                string Lhex = BitConverter.ToString(Lwin1251bytes);
                encLecturer = "%" + Lhex.Replace("-", "%");
            }

            return new StringContent("faculty=0&teacher=" + encLecturer + "&group=" + encsname + "&sdate=" + StartDate + "&edate=" + EndDate + "&n=700");
        }
        private async Task<string> ParseResponse()
        {
            return Encoding.GetEncoding(1251).GetString(await responseMessage.Content.ReadAsByteArrayAsync());
        }

        public static string FirstCharToUpper(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                return input.First().ToString().ToUpper() + input.Substring(1);
            }

            return null;
        }
    }
}

namespace SubjectParser
{
    public sealed class SubjectParser
    {
        private string[] sr = null;
        public enum PType { Stud, Lect }
        private Dictionary<PType, string> patern = null;

        private List<SubjectInstance> list;
        private DayInstance Result { get; set; }
        private Exception err = null;
        public Dictionary<string, string> AR;
        public static SubjectParser Current;
        public SubjectParser()
        {
            err = null;
            list = new List<SubjectInstance>();
            UpdatePatern();
            UpdateAutoreplace();
            defTime = new[]{
              new ScheduleTimeViewItem("08:00-09:20/20", "I"),
            new ScheduleTimeViewItem("09:40-11:00/15", "II"),
            new ScheduleTimeViewItem("11:15-12:35/25", "III"),
            new ScheduleTimeViewItem("13:00-14:20/15", "IV"),
            new ScheduleTimeViewItem("14:35-15:55/10", "V"),
            new ScheduleTimeViewItem("16:05-17:25/10", "VI"),
            new ScheduleTimeViewItem("17:35-18:55/10", "VII"),
            new ScheduleTimeViewItem("19:05-20:25/0", "VIII")
        };
            Current = this;
        }

        public Tuple<DayInstance, Exception> Parse(string dateStr, string lines, bool isLecturer, bool auto, string name)
        {
            err = null;
            List<SubjectInstance> rlist = new List<SubjectInstance>();
            try
            {
                if (patern == null || patern.Count() == 0)
                {
                    throw new Exception("PATTERNS ARE EMPTY");
                }

                sr = lines.Split('\n');
                list = new List<SubjectInstance>();
                foreach (string i in sr)
                {
                    string time = "";
                    if (i.Substring(0, i.IndexOf(' ') + 1).Contains('-'))
                    {
                        time = i.Substring(0, i.IndexOf(' '));
                    }

                    string trash = i.Substring(i.IndexOf(' ') + 1);
                    rlist.AddRange(Parsing(time, trash.Split(new char[] { '\r', '\n' }), isLecturer, auto, name));
                }
                var date = DateTime.ParseExact(dateStr, "dd.MM.yyyy", null);
                return new Tuple<DayInstance, Exception>(new DayInstance(dateStr, date.DayOfWeek.ToString())
                {
                    Subjects = rlist.ToArray()
                }, err);
            }
            catch (Exception) { return new Tuple<DayInstance, Exception>(null, err); }
        }

        public SubjectInstance[] Parsing(string time, string[] subjectS, bool isLecturer, bool auto, string LectName)
        {
            if (patern == null || patern.Count() == 0)
            {
                throw new InvalidOperationException("Emergency!!! PATTERNS ARE EMPTY");
            }

            try
            {
                ScheduleTimeViewItem tm = GetNum(time);
                list = new List<SubjectInstance>();
                Regex r = new Regex(patern[isLecturer ? PType.Lect : PType.Stud], RegexOptions.ECMAScript);
                foreach (var sub in subjectS)
                {
                    MatchCollection mc = r.Matches(sub.Replace('\r', ' '));

                    foreach (Match m in mc)
                    {
                        SubjectInstance _subject = default;
                        GroupCollection g = m.Groups;
                        if (isLecturer)
                        {
                            var sname =   g[5].Value.Trim();
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
                            subgroup = subgroup.TrimStart(new char[] { ' ', '(' }).TrimEnd(new char[] { ' ', ')' });
                            if (string.IsNullOrEmpty(subgroup))
                            {
                                subgroup = "вся група";
                            }

                            _subject = new SubjectInstance(tm.LessonTime, g[6].Value.Trim(), tm.LessonNum.ToString())
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
                }
                                
                return list.ToArray();
            }
            catch (Exception) { return null; }
        }

        private string AutoReplace(string lect, string pat)
        {
            string cg = pat;
            try
            {
                if (pat.Count(x => x == ' ') < 2 && pat.Length < 20)
                {
                    return pat;
                }

                foreach (string k in AR.Keys)
                {
                    if (pat.ToLower().EndsWith(k.ToLower()))
                    {
                        cg = pat.Replace(k, AR[k]);
                    }
                }

                List<string> obj = new List<string>();
                obj = AutoReplaceHelper.SmartSearch(pat);
                if (obj.Any())
                {
                    cg = obj.First();
                }
#if DEBUG
                Console.WriteLine("Replaced: {0} => {1}", pat, cg);
#endif 
            }
            catch { }
            return cg;
        }

        public bool UpdateAutoreplace()
        {
            string f =Path.Combine(Reactor.Current.DirectoryManager[MainApp.Dirs.Subject_Parser],"autoreplace.txt");
            if (File.Exists(f))
            {
                AR = new Dictionary<string, string>();
                StreamReader g = File.OpenText(f);
                string[] h = g.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                Regex r = new Regex(@"(?m)^(.*)\s-\s(.*)", RegexOptions.ECMAScript);
                foreach (string s in h)
                {
                    GroupCollection m = r.Match(s).Groups;
                    AR.Add(m[1].Value, m[2].Value);
                }
                return true;
            }
            return false;
        }

        private bool GetPatternFor(PType type)
        {
            string folder = Reactor.Current.DirectoryManager[MainApp.Dirs.Subject_Parser], fmt = "ddMMyy";
            rev:
            var files = Directory.EnumerateFiles(folder);
            if (files.Count() == 0)
            {
                Console.WriteLine("Awaiting patterns");
                Task.Delay(5000).Wait();
                goto rev;
            }
            DateTime lastDate = new DateTime();
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

            var may = Path.Combine(folder, "pattern_" + lastDate.ToString(fmt) + '_' + type.ToString().ToLower() + ".txt");
            if (File.Exists(may))
            {
                patern.Add(type, File.ReadAllText(may));
                return true;
            }
            return false;
        }
        public bool UpdatePatern()
        {
            patern = new Dictionary<PType, string>();
            List<bool> result = new List<bool>();
            foreach (var a in Enum.GetValues(typeof(PType)).Cast<PType>().ToList())
            {
                result.Add(GetPatternFor(a));
            }
            return !result.Where(x => x == false).Any();
        } 
        private string CheckType(string what)
        {
            what = what.Replace('(', ' ').Replace(')', ' ').TrimStart(' ').TrimEnd(' ');
            if (what.Equals("Л")) { return "Лекція"; }
            else if (what.Equals("Лаб")) { return "Лабораторна робота"; }
            else if (what.Equals("ПрС")) { return "Практичне заняття"; }
            else if (what.Equals("Екз")) { return "Екзамен"; }
            else if (what.Equals("Зал")) { return "Залік"; }
            else if (what.Equals("ТЕСТ")) { return "Тест"; }
            else
            {
                return what;
            }
        }

    };

    /// <summary>
    /// Schedule view presentation of date for displaying in apps 
    /// </summary>
    public class ScheduleTimeViewItem
    {
        public static ScheduleTimeViewItem[] defTime;
        /// <summary>
        /// Delimiter is - and /  so format is "hh:mm-hh:mm/mm" 
        /// </summary>
        public ScheduleTimeViewItem(string time, string num)
        {
            string[] tm = time.Split('/');
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
            if (defTime == null || time == "")
            {
                return null;
            }

            return defTime.Where(x => x.LessonTime.Contains(time.Substring(1, 8))).FirstOrDefault();
        }
        public static ScheduleTimeViewItem GetTime(string num)
        {
            return defTime.Where(x => x.LessonNum == int.Parse(num)).FirstOrDefault();
        }

        private static Dictionary<char, int> _romanMap = new Dictionary<char, int>
        {
            {'I', 1}, {'V', 5}, {'X', 10}, {'L', 50}, {'C', 100}, {'D', 500}, {'M', 1000}
        };
        public static int ConvertRomanToNumber(string text)
        {
            int totalValue = 0, prevValue = 0;
            foreach (char c in text)
            {
                if (!_romanMap.ContainsKey(c))
                {
                    return 0;
                }

                int crtValue = _romanMap[c];
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

    // Not actual - already fixed on server
    public sealed class AutoReplaceHelper
    {
        public static AutoReplaceHelper Current;
        public bool Now = false;
        public AutoReplaceHelper()
        {
            Current = this;
        }
        private static readonly string st = "http://nuwm.edu.ua/";
        public async void Run()
        {
            Dictionary = new Dictionary<string, List<string>>();
            try
            {
                CreateClientRequest request = new CreateClientRequest(st);
                HttpResponseMessage resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                foreach (HtmlNode i in doc.DocumentNode.Descendants("div").Where(x => x.HasClass("hvr")))
                {
                    HtmlNode node = i.Descendants("a").First();
                    string href = st + node.GetAttributeValue("href", "");
                    new Thread(new ParameterizedThreadStart(ParseInstitute)).Start(href);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void ManageAutoReplace()
        {
            string f = "./addons/subjects_parser/autoreplace.txt";
            Dictionary<string, string> fs = SubjectParser.Current.AR;
            string fl = "";
            foreach (KeyValuePair<string, string> i in fs)
            {
                fl += i.Key + " - " + i.Value + "\n";
            }
            File.WriteAllText(f, fl);
        }
        public static List<string> SmartSearch(string name)
        {
            if (name.Contains('.'))
            {
                name = name.Replace('.', ' ');
            }

            string[] namesp = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            List<string> obj = new List<string>();

            foreach (List<string> i in Dictionary.Values)
            {
                IEnumerable<string> gf = i.Where(x => x.ToLower().StartsWith(namesp[0].ToLower()));
                if (gf.Count() > 0)
                {
                    obj.AddRange(gf);
                }
            }
            foreach (string f in namesp.Skip(1))
            {
                if (f == "і" || f == "та")
                {
                    continue;
                }

                List<string> nextGen = new List<string>();
                foreach (string i in obj)
                {
                    if (i.ToLower().Contains(f.ToLower()))
                    {
                        nextGen.Add(i);
                    }
                }
                if (nextGen.Count == 1)
                {
                    if (name.ToLower().Length < nextGen.First().Length)
                    {
                        if (!SubjectParser.Current.AR.ContainsKey(name.ToLower()))
                        {
                            SubjectParser.Current.AR.Add(name.ToLower(), nextGen.First());
                        }

                        return nextGen;
                    }
                }
                obj = nextGen;
            }
            if (obj.Any())
            {
                return obj;
            }

            return new List<string>();
        }
        public async void ParseInstitute(object href)
        {
            try
            {
                CreateClientRequest request = new CreateClientRequest(href as string);
                HttpResponseMessage resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                IEnumerable<HtmlNode> nodet = doc.GetElementbyId("xb-tree-title-id").Descendants()
                    .Where(x => x.InnerText.ToLower().Contains("кафедри"));

                if (nodet.Any())
                {
                    HtmlNode node = nodet.First();
                    IEnumerable<HtmlNode> els = node.Descendants("li");
                    foreach (HtmlNode i in els)
                    {
                        string hrefx = i.Element("a").GetAttributeValue("href", "");
                        if (hrefx.Contains("javascript"))
                        {
                            continue;
                        }

                        if (!hrefx.StartsWith("http"))
                        {
                            hrefx = st + hrefx;
                        }

                        new Thread(new ParameterizedThreadStart(ParseDepartment)).Start(hrefx);

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public async void ParseDepartment(object href)
        {
            try
            {
                CreateClientRequest request = new CreateClientRequest(href as string);
                HttpResponseMessage resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                if (doc.GetElementbyId("sp") == null)
                {
                    return;
                }

                IEnumerable<HtmlNode> node = doc.GetElementbyId("sp").Descendants("a")
                    .Where(x => x.InnerText.ToLower().Contains("дисципліни"));
                HtmlNode f = node.First();
                string hrefx = f.GetAttributeValue("href", "");
                if (hrefx == "#")
                {
                    return;
                }

                if (!hrefx.StartsWith("http"))
                {
                    hrefx = st + hrefx;
                }

                new Thread(new ParameterizedThreadStart(ParsePage)).Start(hrefx);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static Dictionary<string, List<string>> Dictionary;
        private readonly object thisLock = new object();
        public async void ParsePage(object href)
        {
            try
            {
                CreateClientRequest request = new CreateClientRequest(href as string);
                HttpResponseMessage resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                HtmlNode node = doc.DocumentNode.Descendants("table").Where(x => x.HasClass("sklad")).First();

                foreach (HtmlNode i in node.ChildNodes.First().ChildNodes)
                {
                    HtmlNode subj = i.FirstChild;
                    if (subj.InnerText == "Назва дисципліни")
                    {
                        continue;
                    }

                    string lect = System.Net.WebUtility.HtmlDecode(subj.NextSibling.InnerText).TrimEnd(' ').TrimStart(' ');
                    if (lect.Length < 3)
                    {
                        lect = "";
                    }
                    string subject = System.Net.WebUtility.HtmlDecode(subj.InnerText).TrimEnd(' ').TrimStart(' ');
                    lock (thisLock)
                    {
                        if (subject.Length > 2)
                        {
                            subject = subject.Replace("\"", "'").Replace("  ", " ");
                            if (!Dictionary.ContainsKey(lect))
                            {
                                Dictionary.Add(lect, new List<string> { subject });
                            }
                            else
                            {
                                Dictionary[lect].Add(subject);
                            }

                            Dictionary[lect] = Dictionary[lect].Distinct().ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}

namespace HierarchyTime
{
    public partial class DayInstance
    {
        public DayInstance(DateTime date)
        {
            DayName = new CultureInfo("uk-UA").DateTimeFormat.GetDayName(date.DayOfWeek);
            Day = date.ToString("dd.MM.yyyy");
        }
        public DayInstance(string date, string dayName)
        {
            DayName = dayName;
            Day = date;
            DateTime.TryParseExact(Day, "dd.MM.yyyy", null, DateTimeStyles.None, out DateTime dateV);
            DayOfYear = dateV.DayOfYear;
            DayOfWeek = (int)dateV.DayOfWeek - 1;
        }

        public static bool operator ==(DayInstance x, DayInstance y)
        {
            if (Equals(y, null))
            {
                return false;
            }

            if (x.DayName == y.DayName && x.Day == y.Day)
            {
                if (x.Subjects == null && y.Subjects == null)
                {
                    return true;
                }

                for (int i = 0; i < x.Subjects.Count(); i++)
                {

                    if (string.Equals(x.Subjects[i].Classroom, y.Subjects[i].Classroom))
                    {
                        return false;
                    }
                    if (string.Equals(x.Subjects[i].Type, y.Subjects[i].Type))
                    {
                        return false;
                    }
                    if (string.Equals(x.Subjects[i].TimeStamp, y.Subjects[i].TimeStamp))
                    {
                        return false;
                    }
                    if (Equals(x.Subjects[i].Lecturer, y.Subjects[i].Lecturer))
                    {
                        return false;
                    }
                    if (Equals(x.Subjects[i].LessonNum, y.Subjects[i].LessonNum))
                    {
                        return false;
                    }
                    if (string.Equals(x.Subjects[i].Streams, y.Subjects[i].Streams))
                    {
                        return false;
                    }
                    if (string.Equals(x.Subjects[i].SubGroup, y.Subjects[i].SubGroup))
                    {
                        return false;
                    }
                    if (string.Equals(x.Subjects[i].Subject, y.Subjects[i].Subject))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        public static bool operator !=(DayInstance x, DayInstance y)
        {
            if (Equals(y, null))
            {
                return true;
            }

            try
            {
                if (x.DayName == y.DayName && x.Day == y.Day)
                {
                    for (int i = 0; i < x.Subjects.Count(); i++)
                    {

                        if (x.Subjects[i] != y.Subjects[i])
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
            catch (Exception) { return true; }
            return true;
        }

        public override bool Equals(object other)
        {
            if (other is DayInstance == false)
            {
                return false;
            }
            else
            {
                return other != null &&
                    Subjects == ((DayInstance)other).Subjects &&
                       Day == ((DayInstance)other).Day &&
                       DayName == ((DayInstance)other).DayName;
            }
        }
        public override int GetHashCode()
        {
            int hashCode = 0;
            hashCode += hashCode * +EqualityComparer<SubjectInstance[]>.Default.GetHashCode(Subjects);
            hashCode += hashCode * +EqualityComparer<string>.Default.GetHashCode(Day);
            hashCode += hashCode * +EqualityComparer<string>.Default.GetHashCode(DayName);

            return hashCode;
        }
    }
    public partial class SubjectInstance : BaseSubject
    {
        private void NullableAll()
        {
            Classroom =
            Lecturer =
            Streams =
            SubGroup =
            Subject =
            Type =
            TimeStamp = "";
        }
        public SubjectInstance()
        {
            NullableAll();
        }

        public SubjectInstance(string dateTime)
        {
            NullableAll();
            TimeStamp = dateTime;
        }
        public SubjectInstance(string dateTime, string subject, string num)
        {
            NullableAll();
            TimeStamp = dateTime;
            Subject = subject;
            LessonNum = int.Parse(num);
        }
    }
    public partial class WeekInstance
    {
        public WeekInstance() { }
        public WeekInstance(DayInstance dayInit)
        {
            day = new List<DayInstance>
            {
                dayInit
            };
            DateTime.TryParseExact(dayInit.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out DateTime date);
            Sdate = StartOfWeek(date, DayOfWeek.Monday);
            Edate = Sdate.AddDays(6);
            WeekNum = GetIso8601WeekOfYear(Sdate);
        }
        public bool InBounds(DateTime date)
        {
            return (date > Sdate && date < Edate);
        }

        public bool Contains(DayInstance day)
        {
            if (string.IsNullOrEmpty(day.Day))
            {
                return false;
            }

            DateTime.TryParseExact(day.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out DateTime date);
            return InBounds(date);
        }

        public static DateTime CheckIfWeekEnds()
        {
            if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday || DateTime.Now.DayOfWeek == DayOfWeek.Saturday)
            {
                DayOfWeek t = DateTime.Now.DayOfWeek;
                DateTime dn = DateTime.Now;
                return dn.AddDays((t == DayOfWeek.Sunday) ? 1.0 : 2.0);
            }
            return DateTime.UtcNow.AddHours(3);
        }
        public static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = dt.DayOfWeek - startOfWeek;
            if (diff < 0)
            {
                diff += 7;
            }
            return dt.AddDays(-1 * diff).Date;
        }

        public static int GetIso8601WeekOfYear(DateTime time)
        {
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}

