using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DataMasker.Models
{
    /// <summary>
    ///
    /// </summary>
    public class Config
    {
        private Config()
        {

        }
        /// <summary>
        /// Gets or sets the data source config
        /// </summary>
        /// <value>
        /// The data source.
        /// </value>
        [JsonRequired]
        public DataSourceConfig DataSource { get; set; }

        public IList<TableConfig> Tables { get; set; }


        public IList<SqlStatementConfig> SqlStatements { get; set; }

        /// <summary>
        /// Gets or sets the generation configuration.
        /// </summary>
        /// <value>
        /// The data generation configuration.
        /// </value>
        public DataGenerationConfig DataGeneration { get; set; }

        /// <summary>
        /// Path to a separate json file containing the table configuration. A relative path is
        /// resolved against the current directory first and then against the directory of the
        /// main configuration file.
        /// </summary>
        public string TablesConfigPath { get;  set; }

        public static Config Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Configuration file not found: '{filePath}'", filePath);
            }

            Config config = new Config();
            JsonConvert.PopulateObject(File.ReadAllText(filePath), config);

            if (config.Tables == null && string.IsNullOrEmpty(config.TablesConfigPath))
            {
                throw new InvalidOperationException("Must supply table config, either via 'tables' or 'tablesConfigPath' in the configuration file.");
            }

            if (!string.IsNullOrEmpty(config.TablesConfigPath))
            {
                string tablesConfigPath = ResolveTablesConfigPath(filePath, config.TablesConfigPath);
                if (!File.Exists(tablesConfigPath))
                {
                    throw new FileNotFoundException($"Tables configuration file not found: '{config.TablesConfigPath}'", config.TablesConfigPath);
                }
                config.Tables = JsonConvert.DeserializeObject<List<TableConfig>>(File.ReadAllText(tablesConfigPath));
            }

            if (config.DataGeneration == null)
            {
                config.DataGeneration = new DataGenerationConfig();
            }

            // sqlStatements is optional, default to an empty list so callers don't have to null check
            if (config.SqlStatements == null)
            {
                config.SqlStatements = new List<SqlStatementConfig>();
            }
            return config;
        }

        private static string ResolveTablesConfigPath(
            string configFilePath,
            string tablesConfigPath)
        {
            if (Path.IsPathRooted(tablesConfigPath) || File.Exists(tablesConfigPath))
            {
                return tablesConfigPath;
            }

            string configDirectory = Path.GetDirectoryName(Path.GetFullPath(configFilePath));
            if (string.IsNullOrEmpty(configDirectory))
            {
                return tablesConfigPath;
            }

            string relativeToConfig = Path.Combine(configDirectory, tablesConfigPath);
            return File.Exists(relativeToConfig) ? relativeToConfig : tablesConfigPath;
        }
    }
}
