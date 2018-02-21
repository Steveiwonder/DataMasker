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
        /// <param name="batchSize">
        /// When set <see cref="rows"/> will be split into batches of this size and passed to the source
        /// for updating
        /// </param>
        void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            TableConfig config,
            int? batchSize = null);
    }
}