using System;
using System.Collections.Generic;
using DataMasker.Models;

namespace DataMasker.Interfaces
{
    /// <summary>
    /// ISqlStatementDataSource
    /// </summary>
    public interface ISqlStatementDataSource
    {
        /// <summary>
        /// Gets the data.
        /// </summary>
        /// <param name="sqlStatementConfig">The statement configuration.</param>
        /// <returns></returns>
        void Execute(SqlStatementConfig sqlStatementConfig);

    }
}
