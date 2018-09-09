using HelperUtilties;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;


namespace JSON
{
    public class ContentVisualiser
    {
        [JsonProperty("content")]
        public Dictionary<string, List<string>> Content { get; set; }
    }
    public partial class SpecialtiesVisualiser
    {
        [JsonProperty("speciality")]
        public List<Specialty> List { get; set; }
        public partial class Specialty
        {
            [JsonProperty("modulus")]
            public ModulusList Modulus { get; set; }
            [JsonProperty("branch_name")]
            public JSON.Item BranchName { get; set; }
            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("subtitle")]
            public string SubTitle { get; set; }

            [JsonProperty("aver_mark")]
            public string AverMark { get; set; }
            [JsonProperty("aver_mark_calc")]
            public string YourAverMark { get; set; }

            [JsonProperty("code")]
            public string Code { get; set; }
            [JsonProperty("page_content")]
            public ContentVisualiser Content { get; set; }
            [JsonProperty("links")]
            public LinksVisualiser Links { get; set; }
            [JsonProperty("program_provided_by")]
            public TupleVisualiser ChairsProvidesProg { get; set; }
            [JsonProperty("url")]
            public string URL { get; set; }
            [JsonProperty("page_parsing_errors")]
            public List<string> Errors { get; set; }
            public class CalcMarkInfo
            {
                [JsonProperty("min")]
                public double Min { get; set; }
                [JsonProperty("max")]
                public double Max { get; set; }
                [JsonProperty("aver")]
                public double Aver { get; set; }
            }
            public partial class ModulusList
            {
                [JsonProperty("c")]
                public double[] Coef { get; set; }
                [JsonProperty("cn")]
                public string[] CoefName { get; set; }

                [JsonIgnore]
                public string Name { get; set; }
                [JsonIgnore]
                public string Code { get; set; }
            }
        }
    }
    public class LinksVisualiser
    {
        [JsonProperty("link")]
        public Dictionary<string, List<LinkItem>> Links { get; set; }
    }
    public class TupleVisualiser
    {
        [JsonProperty("program_provider")]
        public Tuple<string, List<LinkItem>> ChairsProvidesProg { get; set; }
    }

    public partial class Item : LinkItem
    {
        [JsonProperty("name")]
        public override string Title { get; set; }
        [JsonProperty("url")]
        public override string Url { get; set; }
        [JsonProperty("content")]
        public List<string> Content { get; set; }
    }
    public class LinkItem
    {
        [JsonProperty("title")]
        public virtual string Title { get; set; }
        [JsonProperty("url")]
        public virtual string Url { get; set; }
    }

    public class ScheduleVisualiser
    {
        [JsonProperty("schedule")]
        public object Data { get; set; }
    }

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
    public class ResponseWraper : Response
    {
        [JsonProperty("response")]
        public object ResponseContent { get; set; }
    }

    public enum StatusCode
    {
        Undefined = 1,
        InvalidRequest = 32,
        NotFound = 33,
        AccessDenied = 60,
        DeprecatedMethod = 66,
        ServerSideError = 88,
        Success = 100 
    }
}
