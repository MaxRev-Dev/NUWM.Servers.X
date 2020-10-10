using Newtonsoft.Json;

namespace NUWM.Servers.Core.Calc.Models
{
    public class ModulusList
    {
        public ModulusList()
        {
            Coef = new double[3];
            CoefName = new string[3];
        }

        [JsonIgnore]
        public string Name { get; set; } 

        [JsonProperty("c")]
        public double[] Coef { get; set; }
        [JsonProperty("cn")]
        public string[] CoefName { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
