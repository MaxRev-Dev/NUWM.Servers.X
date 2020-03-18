namespace NUWM.Servers.Core.Calc.Models
{
    public class SpecialtyCodeNormalizer
    {
        public static string Normalize(string code)
        {
            if (code.Length > 3)
            {
                code = code.Trim('0');
            }
            else if (code.Length < 3)
            {
                code = '0' + code;
            }

            return code;
        } 
    }
}