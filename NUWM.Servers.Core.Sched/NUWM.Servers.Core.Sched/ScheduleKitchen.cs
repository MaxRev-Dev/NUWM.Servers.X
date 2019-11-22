using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MaxRev.Utils;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.Sched
{
    /// <summary>
    /// Almost big kitchen.
    /// Manages requests to desk. Uses html parsers and schedule parser.
    /// and returns final representation of weeks and days hierarchy
    /// </summary>
    public sealed class ScheduleKitchen
    {
        #region Vars

        private HttpResponseMessage responseMessage;
        private readonly string sname, lecturer;
        public string StartDate { get; private set; }
        public string EndDate { get; private set; }
        public bool TimetableForLecturer { get; private set; }

        private List<WeekInstance> CurrentDays;
        private List<DayInstance> CurrentParsed;

        private readonly CultureInfo cultureInfo = new CultureInfo("uk-UA");
        private readonly DateTimeFormatInfo dateTimeInfo;
        public Exception R;
        private readonly RetType ReturnType;

        public enum RetType
        {
            weeks,
            days
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
        public ScheduleKitchen(string name, string sdate, string edate, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            R = null;
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
        public ScheduleKitchen(string name, int week, int year, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            R = null;
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
        /// <param name="retType">what to return: weeks or days</param>
        public ScheduleKitchen(string name, int weekfst, int weeklst, in int year, bool isLecturer, RetType retType)
        {
            ReturnType = retType;
            R = null;
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
        private void SetWeek(int week1, int week2, in int year)
        {
            var dateTime = new DateTime(year, 1, 1);
            week1--;
            week2--;
            var curweek = 0;
            while (curweek != week1)
            {
                curweek = WeekInstance.GetIso8601WeekOfYear(dateTime);
                dateTime = dateTime.AddDays(7);
            }

            StartDate = WeekInstance.StartOfWeek(dateTime, DayOfWeek.Monday).ToString("dd.MM.yyyy");
            curweek = 0;
            dateTime = new DateTime(week1 < week2 ? year : year + 1, 1, 1);
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
            var curweek = 0;
            var dateTime = new DateTime(year, 1, 1);
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
        public async Task<object> GetDaysAsync(bool auto)
        {
            var requestUri = new Uri("http://109.87.215.169/cgi-bin/timetable.cgi?n=700");
            try
            {
                string data;
                try
                {
                    using (var request = new Request(requestUri.OriginalString))
                    {
                        responseMessage = await request.PostAsync(JsonData()).ConfigureAwait(false);
                        responseMessage.EnsureSuccessStatusCode();
                        data = await ParseResponseAsync().ConfigureAwait(false);
                    }
                }
                catch
                {
                    throw new OperationCanceledException("Connection failed");
                }

                var denied = data.Contains("Публікація розкладу тимчасово заблокована");
                if (!data.Contains("не знайдено")
                    && !data.Contains("У програмі виникла помилка")
                    && !denied)
                {
                    ParseHtmlToData(data, auto);

                    if (ReturnType == RetType.weeks)
                    {
                        return CurrentDays;
                    }

                    return CurrentParsed;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(data);
                var alert = doc.DocumentNode.Descendants().FirstOrDefault(x => x.HasClass("alert"))?.InnerText;
                if (data.Contains("не знайдено"))
                {
                    R = new InvalidDataException("Not Found");
                }
                else if (denied)
                {
                    R = new DivideByZeroException("Timetable container not found. " + alert);
                }
                else
                {
                    R = new Exception(alert);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (NullReferenceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                R = ex;
            }

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
            var dsd = doc.CreateElement("div");
            var y = doc.DocumentNode.Descendants().Where
                (x => (x.Name == "div" && x.HasClass("jumbotron"))).ToList();
            if (y.Any())
            {
                var footer = y.First().Descendants().Where(x => x.HasClass("container")).FirstOrDefault();
                if (footer != default)
                {
                    foreach (var r in footer.Elements("div"))
                    {
                        dsd.AppendChild(r);
                    }

                    return dsd;
                }
                else
                {
                    R = new DivideByZeroException("Timetable container not found");
                }
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
            data = data.Insert(data.IndexOf("</head>", StringComparison.Ordinal), "<base href=\"#!\">");
            var doc = new HtmlDocument();
            doc.LoadHtml(data);
            CurrentDays = new List<WeekInstance>();
            var tableWrapper = FindTable(doc);

            if (tableWrapper == null)
            {
                return; // No schedule
            }

            CurrentParsed = new List<DayInstance>();
            foreach (var iy in tableWrapper.Descendants("div")
                .Where(x => x.HasClass("col-md-6")))
            {
                CurrentParsed.Add(ParseDay(iy));

                // Fixing lost subjects
                if (iy.NextSibling != null && iy.NextSibling.HasClass("row"))
                {
                    var t = iy.NextSibling;
                    while (t != null && t.HasClass("row"))
                    {
                        var tgt = CurrentParsed.Last().Subjects.ToList();
                        var subj = ParseDayFix(t);
                        if (subj != default)
                        {
                            tgt.AddRange(subj);
                            CurrentParsed.Last().Subjects = tgt.ToArray();
                        }

                        t = t.NextSibling;
                    }
                }
            }

            if (ReturnType == RetType.weeks)
            {
                CurrentDays = new List<WeekInstance>(
                    new[]
                    {
                        new WeekInstance(CurrentParsed[0])
                    });
                for (var it = 1; it < CurrentParsed.Count; it++)
                {
                    var next = CurrentParsed[it];
                    var inf = CurrentDays.Where(x => x.Contains(next)).ToArray();
                    if (inf.Length == 1)
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
        /// <returns></returns>
        private DayInstance ParseDay(HtmlNode node)
        {
            var day = new DayInstance(

                //date && day
                node.ChildNodes.FindFirst("h4").ChildNodes[0].InnerText.Trim(' '),
                node.ChildNodes.FindFirst("h4").Element("small").InnerText
            );

            var table = node.ChildNodes.FindFirst("table");
            var slist = new List<SubjectInstance>();
            foreach (var i in table.ChildNodes.Where(x => x.Name == "tr"))
            {
                var t = i.Elements("td").ToArray();
                var ind = t.ElementAt(0).InnerText;
                var times = t.ElementAt(1).InnerHtml.Replace("<br>", "-");
                var trashed = FixMalformedBlock(t.ElementAt(2).InnerHtml);
                if (!string.IsNullOrEmpty(trashed.Trim()))
                    slist.AddRange(InnerCheckParse(times, trashed, ind));
            }

            day.Subjects = slist.ToArray();
            return day;
        }

        private string FixMalformedBlock(string innerHtml)
        {
            innerHtml = innerHtml
                .Replace("<br> <br>", "\r\n")
                .Replace(" <br> -", "\r\n - ")
                .Replace("<br> ", " ")
                .Replace("<br>", "\r\n");
            var vx = innerHtml.Split("\r\n");
            if (vx.Length > 1 && !vx[0].Contains('(') 
                              && vx[1].Contains('(')
                              && vx[1].Contains(')'))
            {
                return innerHtml.Replace(" - ", "\r\n - ");
            }

            return innerHtml;
        }

        /// <summary>
        /// Html tags not closed properly so here's the FIX.
        /// Parsing lost subjects
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private SubjectInstance[] ParseDayFix(HtmlNode node)
        {
            var tr = node.Element("tr");
            var ind = tr.ChildNodes[0].InnerText;
            var times = tr.ChildNodes[1].InnerHtml.Replace("<br>", "-");
            var trashed = FixMalformedBlock(tr.ChildNodes[2].InnerHtml);
            if (!string.IsNullOrEmpty(trashed.Trim()))
                return InnerCheckParse(times, trashed, ind);
            return default;
        }

        /// <summary>
        /// Subject cell parse handling
        /// </summary>
        /// <param name="times">subject time</param>
        /// <param name="trashed">Trashed string with lecturer, subject and other goods </param>
        /// <param name="ind">Number of lesson</param> 
        /// <returns></returns>
        private SubjectInstance[] InnerCheckParse(string times, string trashed, string ind)
        {
            var subj = new List<SubjectInstance>();
            if (trashed == "Фізичне виховання" || trashed == "Військова підготовка" || trashed == "Вчена рада" ||
                trashed == "Директорат")
            {
                subj.Add(new SubjectInstance(times, trashed, ind));
            }
            else
            {
                if (trashed == "")
                {
                    return subj.ToArray(); /*subj.Add(new SubjectInstance(times, "", ind));*/
                }

                var df = SubjectParser.Current.Parsing(times, trashed.Split("\r\n"), TimetableForLecturer);
                if (df != null)
                {
                    subj.AddRange(df);
                }
            }

            return subj.ToArray();
        }

        #region FALLEN - Not actual now

        private async Task<List<WeekInstance>> GetCacheAsync()
        {
            var f = "./cache/sched/" + (TimetableForLecturer ? "lects" : "groups")
                                        + "/" + (TimetableForLecturer ? lecturer : sname) + ".txt";
            if (File.Exists(f))
            {
                var t = File.OpenText(f);
                return JsonConvert.DeserializeObject<List<WeekInstance>>(await t.ReadToEndAsync()
                    .ConfigureAwait(false));
            }

            return new List<WeekInstance>();
        }

        private async void SaveToCache()
        {
            CurrentDays = new List<WeekInstance>(
                new[]
                {
                    new WeekInstance(CurrentParsed[0])
                });
            for (var it = 1; it < CurrentParsed.Count; it++)
            {
                var next = CurrentParsed[it];
                var inf = CurrentDays.Where(x => x.Contains(next)).ToArray();
                if (inf.Count() == 1)
                {
                    inf.First().day.Add(next);
                }
                else
                {
                    CurrentDays.Add(new WeekInstance(CurrentParsed[it]));
                }
            }

            var t = File.CreateText("./cache/sched/" + (TimetableForLecturer ? "lects" : "groups")
                                                              + "/" + (TimetableForLecturer ? lecturer : sname) +
                                                              ".txt");
            await t.WriteAsync(JsonConvert.SerializeObject(CurrentDays)).ConfigureAwait(false);
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

            var retVal = new List<WeekInstance>();
            foreach (var i in list)
            {
                var week = new WeekInstance();

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

        /// <summary>
        /// Data preparations for posting to web-schedule
        /// </summary>
        /// <returns></returns>
        private HttpContent JsonData()
        {
            string encLecturer = "", encsname = "";
            if (!string.IsNullOrEmpty(sname))
            {
                var Swin1251bytes = Encoding.GetEncoding("windows-1251").GetBytes(sname);
                var Shex = BitConverter.ToString(Swin1251bytes);
                encsname = "%" + Shex.Replace("-", "%");
            }

            if (!string.IsNullOrEmpty(lecturer))
            {
                var Lwin1251bytes = Encoding.GetEncoding("windows-1251").GetBytes(lecturer);
                var Lhex = BitConverter.ToString(Lwin1251bytes);
                encLecturer = "%" + Lhex.Replace("-", "%");
            }
            return new StringContent(
                $"faculty=0&teacher={encLecturer.Replace("%20", "+")}&group={encsname}&sdate={StartDate}&edate={EndDate}&n=700",
                Encoding.UTF8, "application/x-www-form-urlencoded");
        }

        private async Task<string> ParseResponseAsync()
        {
            return Encoding.GetEncoding(1251)
                .GetString(await responseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
        }

        public static string FirstCharToUpper(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                return char.ToUpper(input[0]) + input.Substring(1);
            }

            return null;
        }
    }
}