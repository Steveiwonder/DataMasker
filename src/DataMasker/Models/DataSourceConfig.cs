using Newtonsoft.Json;

namespace DataMasker.Models {
    /// <summary>
    /// DataSourceConfig
    /// </summary>
    public class DataSourceConfig
    {
        /// <summary>
        /// Gets or sets the data source type
        /// </summary>
        /// <value>
        /// The data source.
        /// </value>
        [JsonRequired]
        public DataSourceType Type { get; set; }

        public dynamic Config { get; set; }
    }
}