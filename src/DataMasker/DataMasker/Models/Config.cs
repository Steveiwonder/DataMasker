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
        public DataSourceConfig DataSource { get; set; }

        /// <summary>
        /// Gets or sets the tables.
        /// </summary>
        /// <value>
        /// The tables.
        /// </value>
        public IList<TableConfig> Tables { get; set; }

        /// <summary>
        /// Gets or sets the generation configuration.
        /// </summary>
        /// <value>
        /// The data generation configuration.
        /// </value>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public DataGenerationConfig DataGeneration { get; set; }

        public static Config Load(
            string filePath)
        {
            Config config = new Config();
            JsonConvert.PopulateObject(File.ReadAllText(filePath), config);
            if (config.DataGeneration == null)
            {
                config.DataGeneration = new DataGenerationConfig();
            }
            return config;
        }
    }
}
