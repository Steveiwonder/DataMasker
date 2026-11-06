using System.Collections.Generic;
using DataMasker.Models;

namespace DataMasker.Interfaces
{
    /// <summary>
    /// IDataMasker
    /// </summary>
    public interface IDataMasker
    {

        /// <summary>
        /// Masks the specified object with new data
        /// </summary>
        /// <param name="obj">The object to mask</param>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        IDictionary<string, object> Mask(
            IDictionary<string, object> obj,
            TableConfig tableConfig);

        /// <summary>
        /// Records values that already exist for a column so that a <see cref="ColumnConfig.Unique"/>
        /// column never receives a generated value equal to one already in the table.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="columnName">The column name.</param>
        /// <param name="values">The values already present. Nulls are ignored.</param>
        void SeedUniqueValues(
            string tableName,
            string columnName,
            IEnumerable<object> values);

        /// <summary>
        /// Seeds unique value tracking for every <see cref="ColumnConfig.Unique"/> column in the table
        /// by reading the distinct values currently stored in the data source.
        /// </summary>
        /// <param name="dataSource">The data source to read existing values from.</param>
        /// <param name="tableConfig">The table configuration.</param>
        void SeedUniqueValues(
            IDataSource dataSource,
            TableConfig tableConfig);
    }
}
