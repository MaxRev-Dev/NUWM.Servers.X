using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUWM.Servers.Core.Calc.Extensions;

namespace NUWM.Servers.Core.Calc.Models
{
    interface ICodeItem
    {
        string Code { get; }
    }
    public class BaseItem : ICodeItem
    {
        private string _title;

        public BaseItem()
        {
            PassMarks = new Dictionary<int, double>();
        }

        public bool IsValid() => Modulus.Coef.All(x => x > 0);
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("title")]
        public string Title
        {
            get => _title;
            set {
                _title = value;
                Modulus.Name = _title;
            }
        }

        [JsonProperty("subtitle")]
        public string SubTitle { get; set; }
        [JsonProperty("special")]
        public bool IsSpecial { get; internal set; }
        [JsonProperty("modulus")]
        public ModulusList Modulus { get; set; } = new ModulusList();
        [JsonIgnore]
        public string Branch { get; set; }
        [JsonIgnore]
        public string InnerCode { get; set; }
        [JsonProperty("branch_coef")]
        public double BranchCoef { get; internal set; } = 1.0;
        [JsonProperty("pass_marks")]
        public Dictionary<int, double> PassMarks { get; internal set; }
    }
    public class CalculatedSpecialty : SpecialtyInfo
    {
        public CalculatedSpecialty(SpecialtyInfo specialty)
        {
            // just clone all properties
            specialty.CloneTo(this);
        }
        [JsonProperty("aver_mark_calc")]
        public double YourAverMark { get; set; }
        [JsonProperty("aver_mark")]
        public double PassMark { get; set; }
        [JsonProperty("path")]
        public string CalcPath { get; internal set; }
    }
    [Serializable]
    public class SpecialtyInfo : BaseItem
    {
        internal SpecialtyInfo()
        {

        }
        [JsonProperty("branch_name")]
        public Item BranchName { get; set; }
        [JsonProperty("page_content")]
        public ContentVisualiser Content { get; set; }
        [JsonProperty("links")]
        public LinksVisualiser Links { get; set; }

        [JsonProperty("program_provided_by")]
        public TupleVisualiser ChairsProvidesProg { get; set; } = new TupleVisualiser();
        [JsonProperty("url")]
        public string URL { get; set; }
        [JsonProperty("page_parsing_errors")]
        public List<string> Errors { get; set; }
        public override string ToString()
        {
            return Code + " - " + Title;
        }
    }
    public class CalcMarkInfo
    {
        [JsonProperty("min")]
        public double Min { get; set; }
        [JsonProperty("max")]
        public double Max { get; set; }
        [JsonProperty("aver")]
        public double Aver { get; set; }
    }
    public class Item : LinkItem
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



    public class ContentVisualiser
    {
        [JsonProperty("content")]
        public Dictionary<string, List<string>> Content { get; set; }
    }
    public class SpecialtiesVisualiser
    {
        [JsonProperty("speciality")]
        public IEnumerable<SpecialtyInfo> List { get; set; }
    }

    public class ScheduleVisualiser
    {
        [JsonProperty("schedule")]
        public object Data { get; set; }
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
