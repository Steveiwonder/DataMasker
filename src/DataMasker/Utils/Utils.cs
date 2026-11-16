using System;
using Bogus.DataSets;
using DataMasker.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

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
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in param)
            {
                string name = MakeParamNameSafe(item.Key);
                if (result.ContainsKey(name))
                    throw new InvalidOperationException($"Column names produce the same SQL parameter '{name}'. Rename or alias the conflicting columns.");
                result.Add(name, item.Value);
            }
            return result;
        }

        internal static string MakeParamNameSafe(string paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName))
                throw new ArgumentException("SQL parameter name cannot be empty.", nameof(paramName));
            string name = Regex.Replace(paramName, @"[^\p{L}\p{Nd}_]", "_");
            return char.IsDigit(name[0]) ? "_" + name : name;
        }

        /// <summary>
        /// Quotes an identifier (column, table or schema name) for use in SQL. A name that is
        /// already wrapped in square brackets is used as-is; anything else is wrapped and any
        /// closing bracket inside it is escaped.
        /// </summary>
        internal static string MakeColumnNameSafe(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("An identifier (column, table or schema name) in the configuration is empty.", nameof(identifier));
            }

            if (identifier.Length >= 2 && identifier[0] == '[' && identifier[identifier.Length - 1] == ']')
            {
                return identifier;
            }

            return "[" + identifier.Replace("]", "]]") + "]";
        }

        public static string GetConnectionString(this DataSourceConfig sourceConfig)
        {
            if (sourceConfig == null)
            {
                throw new ArgumentNullException(nameof(sourceConfig));
            }

            if (sourceConfig.Config == null)
            {
                throw new InvalidOperationException("The 'dataSource.config' section is missing from the configuration. Supply a 'connectionString', or 'server', 'name', 'userName' and 'password'.");
            }

            string connectionString;
            if (sourceConfig.Config.connectionString != null && !string.IsNullOrWhiteSpace(sourceConfig.Config.connectionString.ToString()))
            {
                connectionString = sourceConfig.Config.connectionString;
            }
            else
            {
                connectionString = new SqlConnectionStringBuilder
                {
                    UserID = (string)sourceConfig.Config.userName ?? string.Empty,
                    Password = (string)sourceConfig.Config.password ?? string.Empty,
                    DataSource = (string)sourceConfig.Config.server ?? string.Empty,
                    InitialCatalog = (string)sourceConfig.Config.name ?? string.Empty,
                    PersistSecurityInfo = false
                }.ConnectionString;
            }

            return connectionString;
        }
    }
}
