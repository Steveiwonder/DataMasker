using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            config.Validate();
            return config;
        }

        /// <summary>Validates all tables before any masking writes can begin.</summary>
        public void Validate()
        {
            if (DataSource == null || !Enum.IsDefined(typeof(DataSourceType), DataSource.Type))
                throw new InvalidOperationException("A valid dataSource is required.");
            if (Tables == null) throw new InvalidOperationException("Table configuration must contain an array, not null.");
            foreach (var table in Tables)
            {
                DataSources.SqlDataSource.ValidateTable(table);
                global::DataMasker.DataMasker.ValidateTable(table);
                foreach (var column in table.Columns.Where(c => !c.Ignore))
                {
                    if (!Enum.IsDefined(typeof(DataType), column.Type))
                        throw new InvalidOperationException($"Column '{column.Name}' has an unknown data type.");
                    var available = new HashSet<string>(table.Columns.Select(c => c.Name).Concat(new[] { table.PrimaryKeyColumn }));
                    if (column.Type == DataType.Computed && (column.SourceColumns == null || column.SourceColumns.Length == 0 ||
                        column.SourceColumns.Any(c => !available.Contains(c))))
                        throw new InvalidOperationException($"Computed column '{column.Name}' requires SourceColumns present in the table configuration.");
                    if (!string.IsNullOrEmpty(column.UseGenderColumn) && !available.Contains(column.UseGenderColumn))
                        throw new InvalidOperationException($"Gender column '{column.UseGenderColumn}' is missing from table '{table.Name}'.");
                    if (column.Type == DataType.Sql && (DataSource.Type != DataSourceType.SqlServer ||
                        column.SqlValue == null || string.IsNullOrWhiteSpace(column.SqlValue.Query)))
                        throw new InvalidOperationException($"Column '{column.Name}' requires a SQL Server source and sqlValue.query.");
                }
            }
            if (SqlStatements != null && SqlStatements.Any(s => s == null || string.IsNullOrWhiteSpace(s.Statement)))
                throw new InvalidOperationException("Each sqlStatements entry must contain a non-empty statement.");
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
