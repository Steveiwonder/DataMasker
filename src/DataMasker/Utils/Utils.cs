using Bogus.DataSets;
using DataMasker.Models;
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
            return paramName.Replace(" ", "_").Replace("-", "_");
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

        public static string GetConnectionString(this DataSourceConfig sourceConfig)
        {
            string connectionString;
            if (sourceConfig.Config.connectionString != null && !string.IsNullOrWhiteSpace(sourceConfig.Config.connectionString.ToString()))
            {
                connectionString = sourceConfig.Config.connectionString;
            }
            else
            {
                connectionString =
                    $"User ID={sourceConfig.Config.userName};Password={sourceConfig.Config.password};Data Source={sourceConfig.Config.server};Initial Catalog={sourceConfig.Config.name};Persist Security Info=False;";
            }

            return connectionString;
        }
    }
}
