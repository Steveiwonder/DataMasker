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

        /// <summary>
        /// Gets or sets the generation configuration.
        /// </summary>
        /// <value>
        /// The data generation configuration.
        /// </value>
        public DataGenerationConfig DataGeneration { get; set; }
        public string TablesConfigPath { get;  set; }

        public static Config Load(string filePath)  
        {
            Config config = new Config();
            JsonConvert.PopulateObject(File.ReadAllText(filePath), config);

            if(config.Tables == null && string.IsNullOrEmpty(config.TablesConfigPath))
            {
                throw new Exception("Must supply table config, either via TableConfig or TableConfigPath");
            }
            if (!string.IsNullOrEmpty(config.TablesConfigPath))
            {
                config.Tables = JsonConvert.DeserializeObject<List<TableConfig>>(File.ReadAllText(config.TablesConfigPath));
            }
            if (config.DataGeneration == null)
            {
                config.DataGeneration = new DataGenerationConfig();
            }
            return config;
        }
    }
}
