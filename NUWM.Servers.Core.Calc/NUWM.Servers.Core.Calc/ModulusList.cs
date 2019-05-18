using Newtonsoft.Json;

namespace NUWM.Servers.Core.Calc
{
    public class ModulusList
    {
        public ModulusList()
        {
            Coef = new double[3];
            CoefName = new string[3];
        }

        [JsonProperty("c")]
        public double[] Coef { get; set; }
        [JsonProperty("cn")]
        public string[] CoefName { get; set; }

        [JsonIgnore]
        public string Name { get; set; }
        //public static ModulusList GetModulusFromHtml(IEnumerable<HtmlNode> nodes, ModulusList list)
        //{
        //    if (list != null)
        //    {
        //        for (int i = 0; i < 3; i++)
        //        {
        //            var trash = nodes.ElementAt(i).InnerText;
        //            int breaker = trash.IndexOf(' ', trash.IndexOfAny(new char[] { '.', ',' }) - 3);
        //            string num = trash.Substring(breaker + 1).Replace(',', '.');
        //            double coef = double.Parse(num);

        //            list.CoefName[i] = trash.Substring(0, breaker);
        //            list.Coef[i] = coef;
        //        }
        //    }
        //    return list;
        //}
        public override string ToString()
        {
            return Name;
        }
    }
}
