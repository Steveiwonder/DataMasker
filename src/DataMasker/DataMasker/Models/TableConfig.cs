using System.Collections.Generic;

namespace DataMasker.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class TableConfig
    {
        /// <summary>
        /// The name of the table
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// The primary key of the target table, used to update the data
        /// </summary>
        /// <value>
        /// The primary key column.
        /// </value>
        public string PrimaryKeyColumn { get; set; }

        /// <summary>
        /// List of <see cref="ColumnConfig"/>
        /// </summary>
        /// <value>
        /// The columns.
        /// </value>
        public IList<ColumnConfig> Columns { get; set; }
    }
}
