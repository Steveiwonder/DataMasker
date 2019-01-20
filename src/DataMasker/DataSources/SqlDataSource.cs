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
            if (sourceConfig.Config.connectionString!=null && !string.IsNullOrWhiteSpace(sourceConfig.Config.connectionString.ToString()))
            {
                _connectionString = sourceConfig.Config.connectionString;
            }
            else
            {
                _connectionString =
                    $"User ID={sourceConfig.Config.userName};Password={sourceConfig.Config.password};Data Source={sourceConfig.Config.server};Initial Catalog={sourceConfig.Config.name};Persist Security Info=False;";
            }

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
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return (IEnumerable<IDictionary<string, object>>)connection.Query(BuildSelectSql(tableConfig));
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
                connection.Execute(BuildUpdateSql(tableConfig), row, null, commandType: CommandType.Text);
            }
        }


        /// <inheritdoc/>
        public void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            TableConfig config,
            Action<int> updatedCallback)
        {
            int? batchSize = _sourceConfig.UpdateBatchSize;
            if (batchSize == null ||
                batchSize <= 0)
            {
                batchSize = rows.Count();
            }

            IEnumerable<Batch<IDictionary<string, object>>> batches = Batch<IDictionary<string, object>>.BatchItems(
                rows.ToArray(),
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
                    connection.Execute(sql, batch.Items, sqlTransaction, null, CommandType.Text);

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

        /// <summary>
        /// Builds the update SQL.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        private string BuildUpdateSql(
            TableConfig tableConfig)
        {
            string sql = $"UPDATE [{tableConfig.Name}] SET ";

            sql += tableConfig.Columns.GetUpdateColumns();
            sql += $" WHERE [{tableConfig.PrimaryKeyColumn}] = @{tableConfig.PrimaryKeyColumn}";
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
            return $"SELECT  {tableConfig.Columns.GetSelectColumns(tableConfig.PrimaryKeyColumn)} FROM [{tableConfig.Name}]";
        }
    }
}
