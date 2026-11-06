using System;
using System.Collections.Generic;
using DataMasker.Models;

namespace DataMasker.Interfaces
{
    /// <summary>
    /// IDataSource
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// Gets the data. Implementations may stream rows lazily, so callers should
        /// enumerate once and not assume the whole table is in memory.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        IEnumerable<IDictionary<string, object>> GetData(
            TableConfig tableConfig);

        /// <summary>
        /// Updates the row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="tableConfig">The table configuration.</param>
        void UpdateRow(
            IDictionary<string, object> row,
            TableConfig tableConfig);

        /// <summary>
        /// Updates the rows.
        /// </summary>
        /// <param name="rows">The rows.</param>
        /// <param name="rowCount">The total number of rows expected, used for progress reporting.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="updatedCallback">
        /// Called when a number of items have been updated, the value passed is the total items
        /// updated
        /// </param>
        void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            int rowCount,
            TableConfig config,
            Action<int> updatedCallback = null);

        /// <summary>
        /// Gets the number of rows in the table.
        /// </summary>
        int GetCount(TableConfig config);

        /// <summary>
        /// Gets the distinct, non-null values currently stored in a column.
        /// Used to seed unique value tracking so generated values never collide with
        /// values that already exist in the table.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <param name="columnName">The column to read.</param>
        IEnumerable<object> GetDistinctValues(
            TableConfig tableConfig,
            string columnName);
    }
}
