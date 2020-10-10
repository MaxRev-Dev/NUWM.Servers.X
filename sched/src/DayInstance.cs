using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NUWM.Servers.Core.Sched
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
            DateTime.TryParseExact(Day, "dd.MM.yyyy", null, DateTimeStyles.None, out var dateV);
            DayOfYear = dateV.DayOfYear;
            DayOfWeek = (int)dateV.DayOfWeek - 1;
        }

        public static bool operator ==(DayInstance x, DayInstance y)
        {
            if (Equals(x, null) || Equals(y, null))
            {
                return false;
            }

            if (x.DayName == y.DayName && x.Day == y.Day)
            {
                if (x.Subjects == null || y.Subjects == null)
                {
                    return false;
                }

                for (var i = 0; i < x.Subjects.Length; i++)
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
            if (Equals(y, null) || Equals(x, null))
            {
                return true;
            }

            try
            {
                if (x.DayName == y.DayName && x.Day == y.Day)
                {
                    for (var i = 0; i < x.Subjects.Count(); i++)
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

            return Subjects == ((DayInstance)other).Subjects &&
                   Day == ((DayInstance)other).Day &&
                   DayName == ((DayInstance)other).DayName;
        }
        public override int GetHashCode()
        {
            var hashCode = 0;
            hashCode += hashCode * +EqualityComparer<SubjectInstance[]>.Default.GetHashCode(Subjects);
            hashCode += hashCode * +EqualityComparer<string>.Default.GetHashCode(Day);
            hashCode += hashCode * +EqualityComparer<string>.Default.GetHashCode(DayName);

            return hashCode;
        }
    }
}