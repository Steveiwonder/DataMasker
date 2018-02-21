using Newtonsoft.Json;

namespace DataMasker.Models
{
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

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public dynamic Config { get; set; }
        /// <summary>
        /// When true, will not push the changes to the target source, it will execute with a rollback
        /// </summary>
        /// <value>
        ///   <c>true</c> if [dry run]; otherwise, <c>false</c>.
        /// </value>
        public bool DryRun { get; set; }
    }
}