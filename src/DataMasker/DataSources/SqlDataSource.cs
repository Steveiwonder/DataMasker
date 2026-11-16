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
    public class SqlDataSource : IDataSource, IUniqueValueComparerSource
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
            ValidateTable(tableConfig);
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

                // Capture before callers can mutate the yielded dictionaries.
                object nextKey = page.Count == 0 ? null : page[page.Count - 1][tableConfig.PrimaryKeyColumn];
                foreach (IDictionary<string, object> row in page)
                {
                    yield return row;
                }

                if (page.Count < pageSize)
                {
                    yield break;
                }

                lastKey = nextKey;
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
            UpdateRows(new[] { row }, 1, tableConfig, null);
        }

        /// <inheritdoc/>
        public void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            int rowCount,
            TableConfig config,
            Action<int> updatedCallback)
        {
            ValidateTable(config);
            if (!config.Columns.Any(c => !c.Ignore)) return;
            int batchSize = BatchSize;

            IEnumerable<Batch<IDictionary<string, object>>> batches = Batch<IDictionary<string, object>>.BatchItems(
                rows,
                (item, currentBatch) => currentBatch.Count() < batchSize);

            int totalUpdated = 0;
            string updateSql = BuildUpdateSql(config);

            using IDbConnection connection = _connectionFactory();
            connection.Open();

            using DataTable staging = connection is SqlConnection serverConnection
                ? CreateStagingTable(serverConnection, config) : null;

            foreach (Batch<IDictionary<string, object>> batch in batches)
            {
                if (batch.Items.Count == 0)
                {
                    continue;
                }

                using IDbTransaction transaction = connection.BeginTransaction();

                if (connection is SqlConnection sqlConnection)
                {
                    BulkUpdateBatch(sqlConnection, (SqlTransaction)transaction, config, batch.Items, staging);
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
        private DataTable CreateStagingTable(SqlConnection connection, TableConfig config)
        {
            var stagingColumns = new[] { config.PrimaryKeyColumn }.Concat(config.Columns.Where(c => !c.Ignore).Select(c => c.Name));
            string stagingColumnList = string.Join(", ", stagingColumns.Select(Utils.Utils.MakeColumnNameSafe));
            connection.Execute($"SELECT TOP (0) {stagingColumnList} INTO {StagingTableName} FROM {BuildTableName(config)};",
                commandTimeout: CommandTimeout);
            var staging = new DataTable();
            using (IDataReader reader = connection.ExecuteReader($"SELECT TOP (0) {stagingColumnList} FROM {StagingTableName}", commandTimeout: CommandTimeout))
                staging.Load(reader);
            foreach (DataColumn column in staging.Columns)
            {
                column.AutoIncrement = false;
                column.ReadOnly = false;
                column.AllowDBNull = true;
            }
            return staging;
        }

        private void BulkUpdateBatch(
            SqlConnection connection,
            SqlTransaction transaction,
            TableConfig config,
            IList<IDictionary<string, object>> rows,
            DataTable staging)
        {
            string table = BuildTableName(config);
            string primaryKey = Utils.Utils.MakeColumnNameSafe(config.PrimaryKeyColumn);
            List<string> updateColumns = config.Columns.Where(c => !c.Ignore).Select(c => c.Name).ToList();
            List<string> stagingColumns = new List<string> { config.PrimaryKeyColumn };
            stagingColumns.AddRange(updateColumns);
            staging.Clear();
            // Staging is created once, outside batch transactions. Both commit and rollback
            // leave it empty, ready for the next batch.
            connection.Execute($"DELETE FROM {StagingTableName}", transaction: transaction, commandTimeout: CommandTimeout);

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
                $"DELETE FROM {StagingTableName};",
                transaction: transaction,
                commandTimeout: CommandTimeout);
        }

        internal static void ValidateTable(TableConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Utils.Utils.MakeColumnNameSafe(config.Name);
            Utils.Utils.MakeColumnNameSafe(config.PrimaryKeyColumn);
            if (config.Columns == null) throw new InvalidOperationException($"Table '{config.Name}' has no columns configured.");
            if (config.Columns.Any(c => c == null || string.IsNullOrWhiteSpace(c.Name)))
                throw new InvalidOperationException($"Table '{config.Name}' contains an unnamed column.");
            if (config.Columns.Any(c => !c.Ignore && string.Equals(c.Name, config.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Primary key '{config.PrimaryKeyColumn}' cannot be masked. Remove it from columns or set ignore=true.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in config.Columns)
                if (!names.Add(column.Name)) throw new InvalidOperationException($"Duplicate column '{column.Name}' in table '{config.Name}'.");
            Utils.Utils.MakeParamNamesSafe(config.Columns.Select(c => c.Name).Concat(new[] { config.PrimaryKeyColumn })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(n => n, n => (object)null));
        }

        /// <summary>Gets string equality rules from the database column's collation.</summary>
        public IEqualityComparer<object> GetValueComparer(TableConfig tableConfig, string columnName)
        {
            using IDbConnection connection = _connectionFactory();
            if (!(connection is SqlConnection)) return EqualityComparer<object>.Default;
            connection.Open();
            string collation = connection.QuerySingleOrDefault<string>(
                "SELECT c.collation_name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@table) AND c.name = @column",
                new { table = BuildTableName(tableConfig), column = columnName }, commandTimeout: CommandTimeout);
            if (collation == null) return EqualityComparer<object>.Default;
            int lcid = connection.ExecuteScalar<int>("SELECT CONVERT(int, COLLATIONPROPERTY(@collation, 'LCID'))", new { collation }, commandTimeout: CommandTimeout);
            int style = connection.ExecuteScalar<int>("SELECT CONVERT(int, COLLATIONPROPERTY(@collation, 'ComparisonStyle'))", new { collation }, commandTimeout: CommandTimeout);
            return new SqlCollationComparer(lcid, style, collation);
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
