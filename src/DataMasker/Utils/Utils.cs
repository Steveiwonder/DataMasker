using Bogus.DataSets;
using System.Collections.Generic;
using System.Linq;

namespace DataMasker.Utils
{
    public static class Utils
    {
        /// <summary>
        /// Tries to parse gender.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static Name.Gender? TryParseGender(
            string value)
        {
            if (value == null)
            {
                return null;
            }

            value = value.ToLower();

            if (value == "m" ||
                value == "male")
            {
                return Name.Gender.Male;
            }

            if (value == "f" ||
                value == "female")
            {
                return Name.Gender.Female;
            }

            return null;
        }
     
        internal static IDictionary<string, object> MakeParamNamesSafe(IDictionary<string, object> param)
        {
            return param.ToDictionary(d => $"{MakeParamNameSafe(d.Key)}", d => d.Value);
        }

        internal static string MakeParamNameSafe(string paramName)
        {
            return paramName.Replace(" ", "_");
        }

        internal static string MakeColumnNameSafe(string paramName)
        {
            if (paramName[0] != '[')
            {
                paramName = $"[{paramName}";
            }

            if (paramName[paramName.Length-1] != ']')
            {
                paramName = $"{paramName}]";
            }

            return paramName;
        }
    }
}