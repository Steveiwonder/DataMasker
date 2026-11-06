using System;
using Dapper;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using DataMasker.Interfaces;
using DataMasker.Models;
using DataMasker.Utils;

namespace DataMasker.DataSources
{
    /// <summary>
    /// SqlDataSource
    /// </summary>
    /// <remarks>
    /// Rows are read in pages ordered by the primary key so memory use is bounded by the batch
    /// size rather than the table size. Updates against SQL Server are bulk copied into a temp
    /// table and applied with a single UPDATE ... JOIN per batch. Any other
    /// <see cref="IDbConnection"/> (used by the tests) falls back to one UPDATE per row.
    /// </remarks>
    /// <seealso cref="IDataSource"/>
    public class SqlDataSource : IDataSource
    {
        /// <summary>
        /// Batch size used for reading and updating when <see cref="DataSourceConfig.UpdateBatchSize"/> is not set.
        /// </summary>
        public const int DefaultBatchSize = 1000;

        /// <summary>
        /// Command timeout in seconds used when <see cref="DataSourceConfig.CommandTimeout"/> is not set.
        /// </summary>
        public const int DefaultCommandTimeoutSeconds = 600;

        private const string StagingTableName = "#DataMaskerUpdate";

        private readonly DataSourceConfig _sourceConfig;

        private readonly string _connectionString;

        private readonly Func<IDbConnection> _connectionFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDataSource"/> class.
        /// </summary>
        /// <param name="sourceConfig"></param>
        public SqlDataSource(
            DataSourceConfig sourceConfig)
            : this(sourceConfig, null)
        {
        }

        internal SqlDataSource(
            DataSourceConfig sourceConfig,
            Func<IDbConnection> connectionFactory)
        {
            _sourceConfig = sourceConfig;
            _connectionString = connectionFactory == null ? sourceConfig.GetConnectionString() : null;
            _connectionFactory = connectionFactory ?? (() => new SqlConnection(_connectionString));
        }

        private int BatchSize => _sourceConfig.UpdateBatchSize is int size && size > 0 ? size : DefaultBatchSize;

        private int CommandTimeout => _sourceConfig.CommandTimeout is int timeout && timeout >= 0 ? timeout : DefaultCommandTimeoutSeconds;

        /// <summary>
        /// Gets the data, one page at a time ordered by the primary key.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        /// <inheritdoc/>
        public IEnumerable<IDictionary<string, object>> GetData(
            TableConfig tableConfig)
        {
            using IDbConnection connection = _connectionFactory();
            connection.Open();

            bool sqlServer = connection is SqlConnection;
            string columns = tableConfig.Columns.GetSelectColumns(tableConfig.PrimaryKeyColumn);
            string table = BuildTableName(tableConfig);
            string primaryKey = Utils.Utils.MakeColumnNameSafe(tableConfig.PrimaryKeyColumn);
            int pageSize = BatchSize;
            object lastKey = null;

            while (true)
            {
                string where = lastKey == null ? string.Empty : $" WHERE {primaryKey} > @lastKey";
                string sql = sqlServer
                    ? $"SELECT TOP (@take) {columns} FROM {table}{where} ORDER BY {primaryKey}"
                    : $"SELECT {columns} FROM {table}{where} ORDER BY {primaryKey} LIMIT @take";

                var parameters = new DynamicParameters();
                parameters.Add("take", pageSize);
                if (lastKey != null)
                {
                    parameters.Add("lastKey", lastKey);
                }

                List<IDictionary<string, object>> page = connection
                    .Query(sql, parameters, commandTimeout: CommandTimeout, buffered: true)
                    .Cast<IDictionary<string, object>>()
                    .ToList();

                foreach (IDictionary<string, object> row in page)
                {
                    yield return row;
                }

                if (page.Count < pageSize)
                {
                    yield break;
                }

                lastKey = page[page.Count - 1][tableConfig.PrimaryKeyColumn];
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
            using IDbConnection connection = _connectionFactory();
            connection.Open();
            connection.Execute(BuildUpdateSql(tableConfig), Utils.Utils.MakeParamNamesSafe(row), null, CommandTimeout, CommandType.Text);
        }

        /// <inheritdoc/>
        public void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            int rowCount,
            TableConfig config,
            Action<int> updatedCallback)
        {
            int batchSize = BatchSize;

            IEnumerable<Batch<IDictionary<string, object>>> batches = Batch<IDictionary<string, object>>.BatchItems(
                rows,
                (item, currentBatch) => currentBatch.Count() < batchSize);

            int totalUpdated = 0;
            string updateSql = BuildUpdateSql(config);

            using IDbConnection connection = _connectionFactory();
            connection.Open();

            foreach (Batch<IDictionary<string, object>> batch in batches)
            {
                if (batch.Items.Count == 0)
                {
                    continue;
                }

                using IDbTransaction transaction = connection.BeginTransaction();

                if (connection is SqlConnection sqlConnection)
                {
                    BulkUpdateBatch(sqlConnection, (SqlTransaction)transaction, config, batch.Items);
                }
                else
                {
                    connection.Execute(updateSql, batch.Items.Select(Utils.Utils.MakeParamNamesSafe), transaction, CommandTimeout, CommandType.Text);
                }

                if (_sourceConfig.DryRun)
                {
                    transaction.Rollback();
                }
                else
                {
                    transaction.Commit();
                }

                totalUpdated += batch.Items.Count;
                updatedCallback?.Invoke(totalUpdated);
            }
        }

        /// <inheritdoc/>
        public int GetCount(TableConfig config)
        {
            using IDbConnection connection = _connectionFactory();
            connection.Open();
            object count = connection.ExecuteScalar(BuildCountSql(config), commandTimeout: CommandTimeout);
            return Convert.ToInt32(count);
        }

        /// <inheritdoc/>
        public IEnumerable<object> GetDistinctValues(
            TableConfig tableConfig,
            string columnName)
        {
            using IDbConnection connection = _connectionFactory();
            connection.Open();

            string column = Utils.Utils.MakeColumnNameSafe(columnName);
            string sql = $"SELECT DISTINCT {column} FROM {BuildTableName(tableConfig)} WHERE {column} IS NOT NULL";

            using IDataReader reader = connection.ExecuteReader(sql, commandTimeout: CommandTimeout);
            while (reader.Read())
            {
                yield return reader.GetValue(0);
            }
        }

        /// <summary>
        /// Bulk copies a batch into a temp table shaped like the target and applies it with one UPDATE.
        /// </summary>
        private void BulkUpdateBatch(
            SqlConnection connection,
            SqlTransaction transaction,
            TableConfig config,
            IList<IDictionary<string, object>> rows)
        {
            string table = BuildTableName(config);
            string primaryKey = Utils.Utils.MakeColumnNameSafe(config.PrimaryKeyColumn);

            List<string> updateColumns = config.Columns
                .Where(x => !x.Ignore && !string.Equals(x.Name, config.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .ToList();

            if (updateColumns.Count == 0)
            {
                return;
            }

            List<string> stagingColumns = new List<string> { config.PrimaryKeyColumn };
            stagingColumns.AddRange(updateColumns);
            string stagingColumnList = string.Join(", ", stagingColumns.Select(Utils.Utils.MakeColumnNameSafe));

            // SELECT INTO gives the staging table the same column types as the target.
            connection.Execute(
                $"IF OBJECT_ID('tempdb..{StagingTableName}') IS NOT NULL DROP TABLE {StagingTableName}; " +
                $"SELECT TOP (0) {stagingColumnList} INTO {StagingTableName} FROM {table};",
                transaction: transaction,
                commandTimeout: CommandTimeout);

            DataTable staging = new DataTable();
            using (IDataReader schemaReader = connection.ExecuteReader($"SELECT TOP (0) {stagingColumnList} FROM {StagingTableName}", transaction: transaction, commandTimeout: CommandTimeout))
            {
                staging.Load(schemaReader);
            }

            foreach (DataColumn column in staging.Columns)
            {
                column.AutoIncrement = false;
                column.ReadOnly = false;
                column.AllowDBNull = true;
            }

            foreach (IDictionary<string, object> row in rows)
            {
                DataRow dataRow = staging.NewRow();
                for (int i = 0; i < stagingColumns.Count; i++)
                {
                    string columnName = stagingColumns[i];
                    row.TryGetValue(columnName, out object value);
                    try
                    {
                        dataRow[i] = value ?? DBNull.Value;
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidCastException || ex is FormatException)
                    {
                        throw new InvalidOperationException($"Value '{value}' ({value?.GetType().Name}) generated for column '{columnName}' cannot be stored in a column of type {staging.Columns[i].DataType.Name}.", ex);
                    }
                }
                staging.Rows.Add(dataRow);
            }

            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls, transaction))
            {
                bulkCopy.DestinationTableName = StagingTableName;
                bulkCopy.BulkCopyTimeout = CommandTimeout;
                foreach (DataColumn column in staging.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }
                bulkCopy.WriteToServer(staging);
            }

            string setClause = string.Join(", ", updateColumns.Select(x =>
            {
                string safe = Utils.Utils.MakeColumnNameSafe(x);
                return $"t.{safe} = s.{safe}";
            }));

            connection.Execute(
                $"UPDATE t SET {setClause} FROM {table} AS t INNER JOIN {StagingTableName} AS s ON t.{primaryKey} = s.{primaryKey}; " +
                $"DROP TABLE {StagingTableName};",
                transaction: transaction,
                commandTimeout: CommandTimeout);
        }

        private static string BuildTableName(
            TableConfig tableConfig)
        {
            string schema = string.IsNullOrWhiteSpace(tableConfig.Schema) ? "dbo" : tableConfig.Schema;
            return $"{Utils.Utils.MakeColumnNameSafe(schema)}.{Utils.Utils.MakeColumnNameSafe(tableConfig.Name)}";
        }

        /// <summary>
        /// Builds the update SQL.
        /// </summary>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        private static string BuildUpdateSql(
            TableConfig tableConfig)
        {
            string sql = $"UPDATE {BuildTableName(tableConfig)} SET ";

            sql += tableConfig.Columns.GetUpdateColumns();
            sql += $" WHERE {Utils.Utils.MakeColumnNameSafe(tableConfig.PrimaryKeyColumn)} = @{Utils.Utils.MakeParamNameSafe(tableConfig.PrimaryKeyColumn)}";
            return sql;
        }

        private static string BuildCountSql(
            TableConfig tableConfig)
        {
            return $"SELECT COUNT(*) FROM {BuildTableName(tableConfig)}";
        }
    }
}
