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
    /// SqlStatementDataSource
    /// </summary>
    /// <seealso cref="ISqlStatementDataSource"/>
    public class SqlStatementDataSource : ISqlStatementDataSource
    {
        private readonly DataSourceConfig _sourceConfig;

        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDataSource"/> class.
        /// </summary>
        /// <param name="sourceConfig"></param>
        public SqlStatementDataSource(
            DataSourceConfig sourceConfig)
        {
            _sourceConfig = sourceConfig;
            _connectionString = sourceConfig.GetConnectionString();
        }


        /// <summary>
        /// Executes sql statement
        /// </summary>
        /// <param name="sqlStatementConfig">The statement configuration.</param>
        /// <inheritdoc/>
        public void Execute(
            SqlStatementConfig sqlStatementConfig)
        {
            if (!_sourceConfig.DryRun)
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var statementToExecute = sqlStatementConfig.Statement;
                    connection.Execute(statementToExecute);

                }
            }
        }


    }
}
