using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace DataMasker.Models
{
    /// <summary>
    /// TableConfig
    /// </summary>
    public class TableConfig
    {
        /// <summary>
        /// The name of the table
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        [JsonRequired]
        public string Name { get; set; }

        /// <summary>
        /// The primary key of the target table, used to update the data
        /// </summary>
        /// <value>
        /// The primary key column.
        /// </value>
        [JsonRequired]
        public string PrimaryKeyColumn { get; set; }

        /// <summary>
        /// List of <see cref="ColumnConfig"/>
        /// </summary>
        /// <value>
        /// The columns.
        /// </value>
        [JsonRequired]
        public IList<ColumnConfig> Columns { get; set; }

        /// <summary>
        /// The name of the schema in which the table lives
        /// </summary>
        /// <value>
        /// The columns.
        /// </value>
        /// <remarks>Defaults to dbo</remarks>
        [DefaultValue("dbo")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string Schema { get; set; }

    }
}
