using System;
using System.Collections.Generic;
using System.Globalization;

namespace NUWM.Servers.Core.Sched
{
    public partial class WeekInstance
    {
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

            DateTime.TryParseExact(day.Day, "dd.MM.yyyy", null, DateTimeStyles.None, out var date);
            return InBounds(date);
        }

        public static DateTime CheckIfWeekEnds()
        {
            if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday || DateTime.Now.DayOfWeek == DayOfWeek.Saturday)
            {
                var t = DateTime.Now.DayOfWeek;
                var dn = DateTime.Now;
                return dn.AddDays(t == DayOfWeek.Sunday ? 1.0 : 2.0);
            }
            return DateTime.UtcNow.AddHours(3);
        }
        public static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            var diff = dt.DayOfWeek - startOfWeek;
            if (diff < 0)
            {
                diff += 7;
            }
            return dt.AddDays(-1 * diff).Date;
        }

        public static int GetIso8601WeekOfYear(DateTime time)
        {
            var day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}