using Newtonsoft.Json;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NUWM.Servers.Core.Calc.Extensions
{
    internal static class CommonExtensions
    {
        public static T Clone<T>(this T obj)
        {
            // not today BinarySerializer..
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj));
        }
        public static void CloneTo<T>(this T @from, T @to)
        {
            var properties = @from.GetType().GetProperties();
            var t = @to.GetType();
            properties.ToList().ForEach(property =>
            {
                var isPresent = t.GetProperty(property.Name);
                if (isPresent == null) return;
                var value = isPresent.GetValue(@from, null);
                isPresent.SetValue(@to, value, null);
            });
        }

        public static string CaptalizeFirst(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return char.ToUpper(s[0]) + s.Substring(1);
        }
        public static PropertyInfo GetPropertyInfo<TSource, TProperty>(this TSource _, Expression<Func<TSource, TProperty>> propertyLambda)
        {
            var type = typeof(TSource);

            if (!(propertyLambda.Body is MemberExpression member))
                throw new ArgumentException($"Expression '{propertyLambda}' refers to a method, not a property.");

            var propInfo = member.Member as PropertyInfo;
            if (propInfo == null)
                throw new ArgumentException($"Expression '{propertyLambda}' refers to a field, not a property.");
             
            return propInfo;
        }
    }
}