using System;
using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using DataMasker.Interfaces;
using DataMasker.Models;
using DataMasker.Utils;

namespace DataMasker.DataSources
{
    /// <summary>
    /// SqlDataSource
    /// </summary>
    /// <seealso cref="IDataSource"/>
    public class SqlDataSource : IDataSource
    {
        private readonly DataSourceConfig _sourceConfig;

        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDataSource"/> class.
        /// </summary>
        /// <param name="sourceConfig"></param>
        public SqlDataSource(
            DataSourceConfig sourceConfig)
        {
            _sourceConfig = sourceConfig;
            _connectionString = sourceConfig.GetConnectionString();
        }

        /// <summary>
        /// Gets the data.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        /// <inheritdoc/>
        public IEnumerable<IDictionary<string, object>> GetData(
            TableConfig tableConfig)
        {
            SqlConnection connection = new SqlConnection(_connectionString);
            {
                connection.Open();
                return (IEnumerable<IDictionary<string, object>>)connection.Query(BuildSelectSql(tableConfig), buffered: false);
            }
        }

        /// <summary>
        /// Updates the row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="tableConfig">The table configuration.</param>
        /// <inheritdoc/>
        public void UpdateRow(
            IDictionary<string, object> row,
            TableConfig tableConfig)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                connection.Execute(BuildUpdateSql(tableConfig), Utils.Utils.MakeParamNamesSafe(row), null, commandType: CommandType.Text);
            }
        }


        /// <inheritdoc/>
        public void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            int rowCount,
            TableConfig config,
            Action<int> updatedCallback)
        {
            int? batchSize = _sourceConfig.UpdateBatchSize;
            if (batchSize == null ||
                batchSize <= 0)
            {
                batchSize = rowCount;
            }

            IEnumerable<Batch<IDictionary<string, object>>> batches = Batch<IDictionary<string, object>>.BatchItems(
                rows,
                (
                    objects,
                    enumerable) => enumerable.Count() < batchSize);

            int totalUpdated = 0;
          using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                foreach (Batch<IDictionary<string, object>> batch in batches)
                {
                    SqlTransaction sqlTransaction = connection.BeginTransaction();


                    string sql = BuildUpdateSql(config);
                    var safeDictionaries = batch.Items.Select(Utils.Utils.MakeParamNamesSafe);
                    connection.Execute(sql, safeDictionaries, sqlTransaction, null, CommandType.Text);

                    if (_sourceConfig.DryRun)
                    {
                        sqlTransaction.Rollback();
                    }
                    else
                    {
                        sqlTransaction.Commit();
                    }


                    if (updatedCallback != null)
                    {
                        totalUpdated += batch.Items.Count;
                        updatedCallback.Invoke(totalUpdated);
                    }
                }
            }
        }

        public int GetCount(TableConfig config)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var count = connection.ExecuteScalar(BuildCountSql(config));
                return Convert.ToInt32(count);
            }
        }

        /// <summary>
        /// Builds the update SQL.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        private string BuildUpdateSql(
            TableConfig tableConfig)
        {
            string sql = $"UPDATE [{tableConfig.Schema}].[{tableConfig.Name}] SET ";

            sql += tableConfig.Columns.GetUpdateColumns();
            sql += $" WHERE {Utils.Utils.MakeColumnNameSafe(tableConfig.PrimaryKeyColumn)} = @{Utils.Utils.MakeParamNameSafe(tableConfig.PrimaryKeyColumn)}";
            return sql;
        }


        /// <summary>
        /// Builds the select SQL.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        private string BuildSelectSql(
            TableConfig tableConfig)
        {
            return $"SELECT  {tableConfig.Columns.GetSelectColumns(tableConfig.PrimaryKeyColumn)} FROM [{tableConfig.Schema}].[{tableConfig.Name}]";
        }

        private string BuildCountSql(
            TableConfig tableConfig)
        {
            return $"SELECT COUNT(*) FROM [{tableConfig.Schema}].[{tableConfig.Name}]";
        }

    }
}
