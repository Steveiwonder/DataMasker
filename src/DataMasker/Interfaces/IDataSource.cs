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
        /// Gets the data.
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

        int GetCount(TableConfig config);
    }
}
