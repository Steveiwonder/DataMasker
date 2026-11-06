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
    /// SqlStatementDataSource
    /// </summary>
    /// <seealso cref="ISqlStatementDataSource"/>
    public class SqlStatementDataSource : ISqlStatementDataSource
    {
        private readonly DataSourceConfig _sourceConfig;

        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlStatementDataSource"/> class.
        /// </summary>
        /// <param name="sourceConfig"></param>
        public SqlStatementDataSource(
            DataSourceConfig sourceConfig)
        {
            _sourceConfig = sourceConfig;
            _connectionString = sourceConfig.GetConnectionString();
        }

        private int CommandTimeout => _sourceConfig.CommandTimeout is int timeout && timeout >= 0 ? timeout : SqlDataSource.DefaultCommandTimeoutSeconds;

        /// <summary>
        /// Executes sql statement. Skipped entirely when <see cref="DataSourceConfig.DryRun"/> is set.
        /// </summary>
        /// <param name="sqlStatementConfig">The statement configuration.</param>
        /// <inheritdoc/>
        public void Execute(
            SqlStatementConfig sqlStatementConfig)
        {
            if (sqlStatementConfig == null || string.IsNullOrWhiteSpace(sqlStatementConfig.Statement))
            {
                throw new InvalidOperationException($"SQL statement '{sqlStatementConfig?.Name}' has no 'statement' configured.");
            }

            if (_sourceConfig.DryRun)
            {
                return;
            }

            using SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();
            connection.Execute(sqlStatementConfig.Statement, commandTimeout: CommandTimeout);
        }
    }
}
