using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NUWM.Servers.Core.Sched
{
    public class Response
    {
        [JsonProperty("code")]
        public StatusCode Code { get; set; }
        [JsonProperty("cache")]
        public bool Cache { get; set; }
        [JsonProperty("error")]
        public object Error { get; set; }
        [JsonProperty("response")]
        public object Content { get; set; }
    }
    public sealed class ResponseWraper : Response
    {
        [JsonProperty("response")]
        public object ResponseContent { get; set; }
    }

    public class ScheduleVisualiser
    {
        [JsonProperty("schedule")]
        public object Data { get; set; }
    }
    public enum StatusCode
    {
        Undefined = 1,
        InvalidRequest = 32,
        NotFound = 33,
        AccessDenied = 60,
        DeprecatedMethod = 66,
        ServerSideError = 88,
        GatewayTimeout,
        ServerNotResponsing =90,
        Success = 100
    }

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
    public partial class SubjectInstance  
    { 
        [JsonProperty("lecturer")]
        public string Lecturer { get; set; }
        [JsonProperty("subgroup")]
        public string SubGroup { get; set; }
        [JsonProperty("streams_type")]
        public string Streams { get; set; }
        [JsonProperty("lessonNum")]
        public int LessonNum { get; set; }
    }

    public partial class WeekInstance
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
        public string GetSdateString => Sdate.ToString("dd.MM.yyyy");
        [JsonProperty("weekend")]
        public string GetEdateString => Edate.ToString("dd.MM.yyyy");
    }
    public partial class DayInstance
    {
        [JsonProperty("subjects")]
        public SubjectInstance[] Subjects { get; set; }
        [JsonProperty("day")]
        public string Day { get; set; }
        [JsonProperty("dayname")]
        public string DayName { get; set; }
        [JsonProperty("day_of_week")]
        public int DayOfWeek { get; set; }
        [JsonProperty("day_of_year")]
        public int DayOfYear { get; set; }
    }
}