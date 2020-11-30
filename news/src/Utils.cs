using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using MaxRev.Utils;

namespace NUWEE.Servers.Core.News
{
    internal static class Utils
    {
        private static Regex _rgxPhoto = new Regex(@"(?<=photo.).*(?=\/)");

        public static int GetIso8601WeekOfYear(DateTime time)
        {
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        public static bool OriginalImageCheck(ref string value)
        {
            try
            {
                var match = _rgxPhoto.Match(value);
                if (match.Success)
                {
                    var nimg = value.Replace(match.Value,  "0");
                    using (var f = new Request(nimg))
                    {
                        var m = RequestAllocator.Instance.UsingPoolAsync(f)
                            .Result;
                        m.EnsureSuccessStatusCode();
                        if (m.Content.Headers.ContentLength > 100)
                        {
                            value = nimg;
                        }
                    }
                }

                return true;
            }
            catch (HttpRequestException)
            {
                //403 etc.
            }
            catch (AggregateException) { }
            catch (RegexMatchTimeoutException)
            {
                // ignored
            }

            return false;
        }

        /// <exception cref="T:System.Runtime.Serialization.SerializationException">An error has occurred during serialization, such as if an object in the <paramref>graph</paramref> parameter is not marked as serializable.</exception>
        /// <exception cref="T:System.Security.SecurityException">The caller does not have the required permission.</exception>
        public static T DeepCopy<T>(T other)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, other);
                ms.Position = 0;
                return (T)formatter.Deserialize(ms);
            }
        }
    }
}