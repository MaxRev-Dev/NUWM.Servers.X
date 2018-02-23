using HelperUtilties;
using HierarchyTime;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static SubjectParser.ScheduleTimeViewItem;

namespace DataSpace
{
    public class GetData
    {
        private HttpResponseMessage responseMessage;
        private string sname, lecturer;
        public string StartDate { get; private set; }
        public string EndDate { get; private set; }
        public bool TimetableForLecturer { get; private set; }

        private List<WeekInstance> CurrentDays;
        private List<DayInstance> CurrentParsed;

        private CultureInfo cultureInfo = new CultureInfo("uk-UA");
        private DateTimeFormatInfo dateTimeInfo;
        public Exception R = null;
        private RetType ReturnType;
        public enum RetType
        {
            weeks, days
        }
        public GetData(string name, int week, int year, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            if (isLecturer) lecturer = name; else sname = name;
            SetWeek(week, year);
            TimetableForLecturer = isLecturer;
            dateTimeInfo = cultureInfo.DateTimeFormat;
        }
        public GetData(string name, int weekfst, int weeklst, int year, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            if (isLecturer) lecturer = name; else sname = name;
            SetWeek(weekfst, weeklst, year);
            TimetableForLecturer = isLecturer;
            dateTimeInfo = cultureInfo.DateTimeFormat;
        }
        void SetWeek(int week1, int week2, int year)
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
        void SetWeek(int week, int year)
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
        public GetData(string name, string sdate, string edate, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            if (isLecturer) lecturer = name; else sname = name;
            StartDate = sdate;
            EndDate = edate;
            TimetableForLecturer = isLecturer;
            dateTimeInfo = cultureInfo.DateTimeFormat;
        }
        public async Task<object> GetDays(bool auto)
        {
            Uri requestUri = new Uri("http://desk.nuwm.edu.ua/cgi-bin/timetable.cgi");
            try
            {
                CreateClientRequest request = new CreateClientRequest(requestUri.OriginalString);
                responseMessage = await request.PostAsync(JsonData());
                responseMessage.EnsureSuccessStatusCode();
                string data = await ParseResponse();
                if (!data.Contains("не знайдено") && !data.Contains("У програмі виникла помилка"))
                {
                    ParseHtmlToData(data, auto);

                    if (ReturnType == RetType.weeks)
                        return CurrentDays;
                    return CurrentParsed;
                }
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(data);
                if (data.Contains("не знайдено"))
                    R = new InvalidDataException("Not Found");
                else
                    R = new Exception(doc.DocumentNode.Descendants().Where(x => x.HasClass("alert")).First().InnerText);
            }
            catch (OperationCanceledException ex)
            {
                R = ex;
                return "Gateway Timeout";
            }
            catch (NullReferenceException ex)
            {
                R = ex;
                return "Gateway Timeout";
            }
            catch (Exception ex)
            {
                R = ex;
            }
            if (ReturnType == RetType.weeks)
                return new List<WeekInstance>();
            return new List<DayInstance>();
        }

        private HtmlNode FindTable(HtmlDocument doc)
        {
            HtmlNode dsd = doc.CreateElement("div");
            var y = doc.DocumentNode.Descendants().Where
                  (x => (x.Name == "div" && x.HasClass("jumbotron"))).ToList();
            if (y.Any())
            {
                HtmlNode footer = y.First().Descendants().Where(x => x.HasClass("container")).First();
                foreach (var r in footer.Elements("div"))
                    dsd.AppendChild(r);
                return dsd;
            }
            return null;
        }

        public void ParseHtmlToData(string data, bool auto)
        {
            data = data.Insert(data.IndexOf("</head>"), "<base href=\"#!\">");
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(data);
            CurrentDays = new List<WeekInstance>();
            HtmlNode tableWrapper = FindTable(doc);

            if (tableWrapper == null) return; // No schedule
            CurrentParsed = new List<DayInstance>();
            foreach (var iy in tableWrapper.Descendants("div").Where(x => x.HasClass("col-md-6")))
            {
                CurrentParsed.Add(ParseDay(iy, auto));

                // Fixing lost subjects
                if (iy.NextSibling != null && iy.NextSibling.HasClass("row"))
                {
                    var t = iy.NextSibling;
                    while (t != null && t.HasClass("row"))
                    {
                        var tgt = CurrentParsed.Last().Subjects.ToList();
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
                    var next = CurrentParsed[it];
                    var inf = CurrentDays.Where(x => x.Contains(next));
                    if (inf.Count() == 1)
                        inf.First().day.Add(next);
                    else
                        CurrentDays.Add(new WeekInstance(CurrentParsed[it]));
                }
                CurrentParsed.Clear();
            }

        }
        private DayInstance ParseDay(HtmlNode node, bool auto)
        {
            var g = node.ChildNodes.FindFirst("h4").InnerText;
            DayInstance day = new DayInstance()
            {
                //date && day
                Day = node.ChildNodes.FindFirst("h4").ChildNodes[0].InnerText.Trim(' '),
                DayName = node.ChildNodes.FindFirst("h4").Element("small").InnerText
            };

            var table = node.ChildNodes.FindFirst("table");
            List<SubjectInstance> slist = new List<SubjectInstance>();
            foreach (var i in table.ChildNodes.Where(x => x.Name == "tr"))
            {
                var t = i.Elements("td");
                var ind = t.ElementAt(0).InnerText;
                var times = t.ElementAt(1).InnerHtml.Replace("<br>", "-");
                var trashed = t.ElementAt(2).InnerText;
                slist.AddRange(InnerCheckParse(times, trashed, ind, auto));
            }
            day.Subjects = slist.ToArray();
            return day;
        }

        private SubjectInstance[] ParseDayFix(HtmlNode node, bool auto)
        {
            var tr = node.Element("tr");
            var ind = tr.ChildNodes[0].InnerText;
            var times = tr.ChildNodes[1].InnerHtml.Replace("<br>", "-");
            var trashed = tr.ChildNodes[2].InnerText;
            return InnerCheckParse(times, trashed, ind, auto);
        }
        private SubjectInstance[] InnerCheckParse(string times, string trashed, string ind, bool auto)
        {
            List<SubjectInstance> subj = new List<SubjectInstance>();
            if (trashed == "Фізичне виховання" || trashed == "Військова підготовка")
            {
                subj.Add(new SubjectInstance(times, trashed, ind));
            }
            else
            {
                if (trashed == "") return subj.ToArray(); /*subj.Add(new SubjectInstance(times, "", ind));*/
                else
                    subj.AddRange(Server.Server.CurrentSubjectParser.Parsing(times, trashed, TimetableForLecturer, auto, this.lecturer));
            }
            return subj.ToArray();
        }

        #region FALLEN
        async Task<List<WeekInstance>> GetCache()
        {
            var f = "./cache/sched/" + (TimetableForLecturer ? "lects" : "groups")
                   + "/" + (TimetableForLecturer ? lecturer : this.sname) + ".txt";
            if (File.Exists(f))
            {
                var t = File.OpenText(f);
                return JsonConvert.DeserializeObject<List<WeekInstance>>(await t.ReadToEndAsync());
            }
            return new List<WeekInstance>();
        }
        async void SaveToCache()
        {
            CurrentDays = new List<WeekInstance>(
                new[] { new WeekInstance(CurrentParsed[0])
                });
            for (int it = 1; it < CurrentParsed.Count; it++)
            {
                var next = CurrentParsed[it];
                var inf = CurrentDays.Where(x => x.Contains(next));
                if (inf.Count() == 1)
                    inf.First().day.Add(next);
                else
                    CurrentDays.Add(new WeekInstance(CurrentParsed[it]));
            }
            var t = File.CreateText("./cache/sched/" + (TimetableForLecturer ? "lects" : "groups")
                + "/" + (TimetableForLecturer ? lecturer : this.sname) + ".txt");
            await t.WriteAsync(JsonConvert.SerializeObject(CurrentDays));
            t.Close();
        }
        #endregion

        private List<WeekInstance> FillEmpty(List<WeekInstance> list)
        {

            List<WeekInstance> retVal = new List<WeekInstance>();
            foreach (var i in list)
            {
                WeekInstance week = new WeekInstance();

                foreach (var d in i.day)
                {
                    if (d.DayName == null || d.Day == null)
                    {
                        if (i.day.IndexOf(d) > 0)
                        {
                            var prev = i.day[i.day.IndexOf(d) - 1];
                            //var cday = DateTime.Parse(prev.Day).AddDays(1);
                            DateTime.TryParseExact(prev.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out var tmp);
                            var cday = tmp.AddDays(1);

                            d.Day = cday.ToString("dd.MM.yyyy");
                            d.DayName = FirstCharToUpper(dateTimeInfo.GetDayName(cday.DayOfWeek));
                        }
                        else
                        {
                            var next = i.day[i.day.IndexOf(d) + 1];
                            //var cday = DateTime.Parse(next.Day).AddDays(-1);
                            DateTime.TryParseExact(next.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out var tmp);
                            var cday = tmp.AddDays(1);

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
        private HttpContent JsonData()
        {
            string encLecturer = "", encsname = "";
            if (!String.IsNullOrEmpty(sname))
            {
                byte[] Swin1251bytes = Encoding.GetEncoding("windows-1251").GetBytes(sname);
                string Shex = BitConverter.ToString(Swin1251bytes);
                encsname = "%" + Shex.Replace("-", "%");
            }
            if (!String.IsNullOrEmpty(lecturer))
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
            if (!String.IsNullOrEmpty(input))
                return input.First().ToString().ToUpper() + input.Substring(1);
            return null;
        }
    }
}

namespace SubjectParser
{
    public class SubjectParser
    {
        string[] sr = null;
        string[] patern = null;

        private List<SubjectInstance> list;
        private DayInstance Result { get; set; }
        private Exception err = null;
        public Dictionary<string, string> AR;
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
        }

        public Tuple<DayInstance, Exception> Parse(string lines, bool isLecturer, bool auto, string name)
        {
            err = null;
            List<SubjectInstance> rlist = new List<SubjectInstance>();
            try
            {
                sr = lines.Split('\n');
                list = new List<SubjectInstance>();
                foreach (var i in sr)
                {
                    var time = "";
                    if (i.Substring(0, i.IndexOf(' ') + 1).Contains('-'))
                        time = i.Substring(0, i.IndexOf(' '));
                    var trash = i.Substring(i.IndexOf(' ') + 1);
                    rlist.AddRange(Parsing(time, trash, isLecturer, auto, name));
                }
                return new Tuple<DayInstance, Exception>(new DayInstance()
                {
                    Subjects = rlist.ToArray()
                }, err);
            }
            catch (Exception) { return new Tuple<DayInstance, Exception>(null, err); }
        }

        public SubjectInstance[] Parsing(string time, string subjectS, bool isLecturer, bool auto, string LectName)
        {
            try
            {
                list = new List<SubjectInstance>();
                var r = new Regex(patern[isLecturer ? 1 : 0], RegexOptions.ECMAScript);
                var m = r.Matches(subjectS.Replace('\r', ' '));
                if (m.Count == 0 && !isLecturer)
                { // Subgroups
                    m = new Regex(patern[2], RegexOptions.ECMAScript).Matches(subjectS);
                    foreach (Match match in m)
                    {
                        SubjectInstance _subject = new SubjectInstance();
                        var g = match.Groups;
                        var audit = (g[1].Value.Contains('.') ? "" : g[1].Value);
                        var stream_or_groups_type = "";
                        var groups_streams = g[5].Value.Contains('.') ? g[5].Value : "";
                        var lecturer = g[3].Value;
                        var subject = g[4].Value.TrimStart(' ').TrimEnd(' ');
                        if (string.IsNullOrEmpty(subject))
                            subject = g[5].Value;
                        var type = (g[7].Value != "" ? g[7].Value : g[5].Value).TrimStart(' ').TrimEnd(' ');


                        if (string.IsNullOrEmpty(time) && list.Count > 0)
                            time = list.Last().TimeStamp;
                        var tm = GetNum(time);
                        _subject.Subject = subject.TrimStart(' ').TrimEnd(' ');
                        _subject.SubGroup = groups_streams.TrimStart(new char[] { ' ', '(' }).TrimEnd(new char[] { ' ', ')' });
                        _subject.Classroom = audit.TrimStart(' ').TrimEnd(' ');
                        _subject.Lecturer = lecturer.TrimStart(' ').TrimEnd(' ');
                        _subject.Streams = stream_or_groups_type.TrimStart(new char[] { ' ', '(' }).TrimEnd(new char[] { ' ', ')' });
                        _subject.TimeStamp = (tm == null) ? "null" : tm.LessonTime;
                        _subject.LessonNum = (tm == null) ? -1 : tm.LessonNum;
                        _subject.Type = CheckType(type);
                        if (AR != null && auto)
                            _subject.Subject = AutoReplace(lecturer, _subject.Subject);

                        list.Add(_subject);
                    }
                }
                else
                    foreach (Match match in m)
                    {
                        var hj = match.Groups[0].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (hj.Count() < 1 || match.Value == " ") continue;
                        var tm = GetNum(time);

                        var g = match.Groups;

                        SubjectInstance _subject = new SubjectInstance();
                        if (g[0].Value.Contains("Фізичне ви") || g[0].Value.Contains("Військова"))
                        {

                            list.Add(new SubjectInstance(tm.LessonTime, g[0].Value, tm.LessonNum.ToString()));
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(time) && list.Count > 0)
                                time = list.Last().TimeStamp;
                            var audit = (g[1].Value.Contains('.') ? "" : g[1].Value);
                            if (string.IsNullOrEmpty(audit) && list.Count > 0)
                                audit = list.Last().Classroom;

                            var lecturer = (g[2].Value == "") ? g[1].Value : g[2].Value;
                            if (string.IsNullOrEmpty(lecturer) && list.Count > 0)
                                lecturer = list.Last().Lecturer;
                            else if (!lecturer.Contains('.'))
                                lecturer = g[1].Value;
                            var stream_or_groups_type = !isLecturer ? g[7].Value : (g[3].Value/*string.IsNullOrEmpty(g[3].Value) ? g[4].Value : g[3].Value*/);
                            var groups_streams = !isLecturer ? g[8].Value : g[4].Value;
                            var subject = (!isLecturer ? g[6].Value : g[2].Value).TrimStart(' ').TrimEnd(' ');
                            if (string.IsNullOrEmpty(subject))
                                subject = g[5].Value;
                            var type = (isLecturer ? g[5].Value : g[9].Value).TrimStart(' ').TrimEnd(' ');

                            if ((type.Count(f => f == '(' || f == ')') % 2 != 0))
                            {
                                var t = type.Substring(type.IndexOf('(', 3));
                                _subject.Type = CheckType(t);
                                _subject.Subject += subject + " " + type.Substring(0, type.IndexOf('(', 3));
                            }
                            else
                            {
                                if (!isLecturer)
                                    _subject.Type = CheckType(g[9].Value);
                                else _subject.Type = CheckType(g[5].Value);
                                _subject.Subject = subject.TrimStart(' ').TrimEnd(' ');
                            }
                            _subject.SubGroup = groups_streams.TrimStart(new char[] { ' ', '(' }).TrimEnd(new char[] { ' ', ')' });
                            _subject.Classroom = audit.TrimStart(' ').TrimEnd(' ');
                            if (!isLecturer)
                                _subject.Lecturer = lecturer.TrimStart(' ').TrimEnd(' ');
                            _subject.Streams = stream_or_groups_type.TrimStart(new char[] { ' ', '(' }).TrimEnd(new char[] { ' ', ')' });
                            _subject.TimeStamp = (tm == null) ? "null" : tm.LessonTime;
                            _subject.LessonNum = (tm == null) ? -1 : tm.LessonNum;

                            if (AR != null && auto)
                                _subject.Subject = AutoReplace(lecturer, _subject.Subject);
                            if (isLecturer)
                            {
                                if (!string.IsNullOrEmpty(g[6].Value.Trim(' ')) && !g[6].Value.Trim(' ').Contains("("))
                                {
                                    list.Add(new SubjectInstance(tm.LessonTime, g[6].Value, tm.LessonNum.ToString()));
                                }
                                if (Server.Server.Fix22_lecturerName)
                                    try
                                    {
                                        
                                            var tf = LectName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                            _subject.Lecturer = tf[0] + " " + tf[1][0] + "." + tf[2][0] + ".";
                                      
                                    }
                                    catch { }
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
                if (pat.Count(x => x == ' ') < 2 && pat.Length < 20) return pat;
                foreach (var k in AR.Keys)
                    if (pat.ToLower().EndsWith(k.ToLower()))
                        cg = pat.Replace(k, AR[k]);
                List<string> obj = new List<string>();
                obj = AutoReplaceHelper.SmartSearch(pat);
                if (obj.Any())
                    cg = obj.First();
#if DEBUG
                Console.WriteLine("Replaced: {0} => {1}", pat, cg);
#endif 
            }
            catch { }
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
        public bool UpdatePatern()
        {
            var f1 = "./addons/subjects_parser/pattern.txt";
            var f2 = "./addons/subjects_parser/pattern_lect.txt";
            var f3 = "./addons/subjects_parser/subgroup_pattern.txt";
            patern = new string[3];
            if (File.Exists(f1))
            {
                var t = File.ReadAllText(f1);
                patern[0] = t;
            }
            if (File.Exists(f2))
            {
                var t = File.ReadAllText(f2);
                patern[1] = t;
            }
            if (File.Exists(f3))
            {
                var t = File.ReadAllText(f3);
                patern[2] = t;
                return true;
            }
            return false;
        }
        public Tuple<DayInstance, Exception> Test()
        {
            string lines = "";
            try
            {
                sr = lines.Split('\n');
                list = new List<SubjectInstance>();
                foreach (var i in sr)
                {
                    var time = "08:00-09:20";
                    if (i.Substring(i.IndexOf(' ') + 1).Contains('-'))
                        time = i.Substring(0, i.IndexOf(' '));
                    var trash = i.Substring(i.IndexOf(' ') + 1);
                    list.AddRange(Parsing(time, trash, false, true, ""));
                }
                return new Tuple<DayInstance, Exception>(new DayInstance()
                {
                    Subjects = list.ToArray()
                }, err);
            }
            catch (Exception) { return new Tuple<DayInstance, Exception>(null, err); }
        }
        private string CheckType(string what)
        {
            what = what.Replace('(', ' ').Replace(')', ' ').TrimStart(' ').TrimEnd(' ');
            if (what.Equals("Л")) { return "Лекція"; }
            else if (what.Equals("Лаб")) { return "Лабораторна"; }
            else if (what.Equals("ПрС")) { return "Практична"; }
            else if (what.Equals("Екз")) { return "Екзамен"; }
            else if (what.Equals("Зал")) { return "Залік"; }
            else if (what.Equals("ТЕСТ")) { return "Тест"; }
            else
                return what;
        }

    };
    public class ScheduleTimeViewItem
    {
        public static ScheduleTimeViewItem[] defTime;
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
                AfterBreak = String.Format("{0} {1}", tm[1], "minutes");
        }
        public int LessonNum { get; set; }
        public string LessonNumRom { get; set; }
        public string LessonTime { get; set; }
        public string AfterBreak { get; set; }

        public static ScheduleTimeViewItem GetNum(string time)
        {
            if (defTime == null || time == "") return null;
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
            foreach (var c in text)
            {
                if (!_romanMap.ContainsKey(c))
                    return 0;
                var crtValue = _romanMap[c];
                totalValue += crtValue;
                if (prevValue != 0 && prevValue < crtValue)
                {
                    if (prevValue == 1 && (crtValue == 5 || crtValue == 10)
                        || prevValue == 10 && (crtValue == 50 || crtValue == 100)
                        || prevValue == 100 && (crtValue == 500 || crtValue == 1000))
                        totalValue -= 2 * prevValue;
                    else
                        return 0;
                }
                prevValue = crtValue;
            }
            return totalValue;
        }

    }
    public class AutoReplaceHelper
    {
        public static AutoReplaceHelper Current;
        public bool Now = false;
        public AutoReplaceHelper()
        {
            Current = this;
        }
        private static
                string st = "http://nuwm.edu.ua/";
        public async void Run()
        {
            Dictionary = new Dictionary<string, List<string>>();
            try
            {
                CreateClientRequest request = new CreateClientRequest(st);
                var resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                foreach (var i in doc.DocumentNode.Descendants("div").Where(x => x.HasClass("hvr")))
                {
                    var node = i.Descendants("a").First();
                    var href = st + node.GetAttributeValue("href", "");
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
            var f = "./addons/subjects_parser/autoreplace.txt";
            var fs = Server.Server.CurrentSubjectParser.AR;
            string fl = "";
            foreach (var i in fs)
            {
                fl += i.Key + " - " + i.Value + "\n";
            }
            File.WriteAllText(f, fl);
        }
        public static List<string> SmartSearch(string name)
        {
            if (name.Contains('.'))
                name = name.Replace('.', ' ');

            var namesp = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            List<string> obj = new List<string>();

            foreach (var i in Dictionary.Values)
            {
                var gf = i.Where(x => x.ToLower().StartsWith(namesp[0].ToLower()));
                if (gf.Count() > 0)
                    obj.AddRange(gf);
            }
            foreach (var f in namesp.Skip(1))
            {
                if (f == "і" || f == "та") continue;
                List<string> nextGen = new List<string>();
                foreach (var i in obj)
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
                        if (!Server.Server.CurrentSubjectParser.AR.ContainsKey(name.ToLower()))
                            Server.Server.CurrentSubjectParser.AR.Add(name.ToLower(), nextGen.First());
                        return nextGen;
                    }
                }
                obj = nextGen;
            }
            if (obj.Any())
                return obj;
            return new List<string>();
        }
        public async void ParseInstitute(object href)
        {
            try
            {
                CreateClientRequest request = new CreateClientRequest(href as string);
                var resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                var nodet = doc.GetElementbyId("xb-tree-title-id").Descendants()
                    .Where(x => x.InnerText.ToLower().Contains("кафедри"));

                if (nodet.Any())
                {
                    var node = nodet.First();
                    var els = node.Descendants("li");
                    foreach (var i in els)
                    {
                        var hrefx = i.Element("a").GetAttributeValue("href", "");
                        if (hrefx.Contains("javascript")) continue;
                        if (!hrefx.StartsWith("http")) hrefx = st + hrefx;
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
                var resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                if (doc.GetElementbyId("sp") == null)
                    return;
                var node = doc.GetElementbyId("sp").Descendants("a")
                    .Where(x => x.InnerText.ToLower().Contains("дисципліни"));
                var f = node.First();
                var hrefx = f.GetAttributeValue("href", "");
                if (hrefx == "#") return;
                if (!hrefx.StartsWith("http")) hrefx = st + hrefx;
                new Thread(new ParameterizedThreadStart(ParsePage)).Start(hrefx);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static Dictionary<string, List<string>> Dictionary;
        private Object thisLock = new Object();
        public async void ParsePage(object href)
        {
            try
            {
                CreateClientRequest request = new CreateClientRequest(href as string);
                var resp = await request.GetAsync();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await resp.Content.ReadAsStringAsync());
                var node = doc.DocumentNode.Descendants("table").Where(x => x.HasClass("sklad")).First();

                foreach (var i in node.ChildNodes.First().ChildNodes)
                {
                    var subj = i.FirstChild;
                    if (subj.InnerText == "Назва дисципліни") continue;
                    var lect = System.Net.WebUtility.HtmlDecode(subj.NextSibling.InnerText).TrimEnd(' ').TrimStart(' ');
                    if (lect.Length < 3)
                    {
                        lect = "";
                    }
                    var subject = System.Net.WebUtility.HtmlDecode(subj.InnerText).TrimEnd(' ').TrimStart(' ');
                    lock (thisLock)
                    {
                        if (subject.Length > 2)
                        {
                            subject = subject.Replace("\"", "'").Replace("  ", " ");
                            if (!Dictionary.ContainsKey(lect))
                            {
                                Dictionary.Add(lect, new List<string> { subject });
                            }
                            else Dictionary[lect].Add(subject);

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
    public class BaseSubject
    {
        [JsonProperty("time")]
        public string TimeStamp { get; set; }
        [JsonProperty("classroom")]
        public string Classroom { get; set; }
        [JsonProperty("subject")]
        public string Subject { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
    }
    public class DayInstance
    {
        public DayInstance() { }

        public DayInstance(int count)
        {
            this.Subjects = new SubjectInstance[count];
        }
        public static bool operator ==(DayInstance x, DayInstance y)
        {
            if (Equals(y, null)) return false;
            if (x.DayName == y.DayName && x.Day == y.Day)
            {
                if (x.Subjects == null && y.Subjects == null) return true;
                for (int i = 0; i < x.Subjects.Count(); i++)
                {

                    if (String.Equals(x.Subjects[i].Classroom, y.Subjects[i].Classroom))
                    {
                        return false;
                    }
                    if (String.Equals(x.Subjects[i].Type, y.Subjects[i].Type))
                    {
                        return false;
                    }
                    if (String.Equals(x.Subjects[i].TimeStamp, y.Subjects[i].TimeStamp))
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
                    if (String.Equals(x.Subjects[i].Streams, y.Subjects[i].Streams))
                    {
                        return false;
                    }
                    if (String.Equals(x.Subjects[i].SubGroup, y.Subjects[i].SubGroup))
                    {
                        return false;
                    }
                    if (String.Equals(x.Subjects[i].Subject, y.Subjects[i].Subject))
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
            if (Equals(y, null)) return true;
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
                return false;
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
            var hashCode = 0;
            hashCode += hashCode * +EqualityComparer<SubjectInstance[]>.Default.GetHashCode(Subjects);
            hashCode += hashCode * +EqualityComparer<string>.Default.GetHashCode(Day);
            hashCode += hashCode * +EqualityComparer<string>.Default.GetHashCode(DayName);

            return hashCode;
        }
        [JsonProperty("subjects")]
        public SubjectInstance[] Subjects { get; set; }
        [JsonProperty("day")]
        public string Day { get; set; }
        [JsonProperty("dayname")]
        public string DayName { get; set; }

    }
    public class SubjectInstance : BaseSubject
    {
        public SubjectInstance()
        {
            this.Classroom =
            this.Lecturer =
            this.Streams =
            this.SubGroup =
            this.Subject =
            this.Type =
            this.TimeStamp = "";
        }
        public SubjectInstance(string dateTime)
        {
            this.TimeStamp = dateTime;
            this.Classroom = this.Subject = this.Lecturer = this.Streams = this.SubGroup = this.Type = "";
        }
        public SubjectInstance(string dateTime, string classRoom, string subject, string lecturer, string subgroup, string type)
        {
            this.TimeStamp = dateTime;
            this.Classroom = classRoom;
            this.Subject = subject;
            this.Lecturer = lecturer;
            this.SubGroup = SubGroup;
            this.Type = type;
        }
        public SubjectInstance(string dateTime, string subject, string num)
        {
            this.TimeStamp = dateTime;
            this.Subject = subject;
            this.LessonNum = int.Parse(num);
            this.Classroom = this.Lecturer = this.Streams = this.SubGroup = this.Type = "";
        }

        [JsonProperty("lecturer")]
        public string Lecturer { get; set; }
        [JsonProperty("subgroup")]
        public string SubGroup { get; set; }
        [JsonProperty("streams_type")]
        public string Streams { get; set; }
        [JsonProperty("lessonNum")]
        public int LessonNum { get; set; }
    }
    public class WeekInstance
    {
        [JsonIgnore]
        public DateTime Sdate { get; set; }
        [JsonIgnore]
        public DateTime Edate { get; set; }
        [JsonProperty("weeknum")]
        public int WeekNum { get; set; }
        [JsonProperty("days")]
        public List<DayInstance> day;
        [JsonProperty("weekstart")]
        public string GetSdateString { get { return Sdate.ToString("dd.MM.yyyy"); } }
        [JsonProperty("weekend")]
        public string GetEdateString { get { return Edate.ToString("dd.MM.yyyy"); } }

        public bool InBounds(DateTime date) { return (date > Sdate && date < Edate); }
        public bool Contains(DayInstance day)
        {
            if (string.IsNullOrEmpty(day.Day)) return false;
            DateTime.TryParseExact(day.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out var date);
            return InBounds(date);
        }
        public WeekInstance(int x)
        {
            day = new List<DayInstance>();
            for (int i = 0; i < 6; i++)
            {
                day.Add(new DayInstance());
            }
        }
        public WeekInstance() { }
        public WeekInstance(DayInstance dayInit)
        {
            day = new List<DayInstance>
            {
                dayInit
            };
            DateTime.TryParseExact(dayInit.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out var date);
            Sdate = StartOfWeek(date, DayOfWeek.Monday);
            Edate = Sdate.AddDays(6);
            WeekNum = GetIso8601WeekOfYear(Sdate);
        }

        public static DateTime CheckIfWeekEnds()
        {
            if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday || DateTime.Now.DayOfWeek == DayOfWeek.Saturday)
            {
                var t = DateTime.Now.DayOfWeek;
                var dn = DateTime.Now;
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