using System;
using Bogus.DataSets;
using DataMasker.Interfaces;
using DataMasker.Models;
using System.Data;
using Dapper;
using System.Collections.Generic;

namespace DataMasker
{
  /// <summary>
  /// Provides column values by running a scalar query per row.
  /// </summary>
  /// <remarks>
  /// The connection is opened on first use and left open for the lifetime of the provider so
  /// that each row does not pay for a connection open/close. The caller owns the connection
  /// and is responsible for disposing it.
  /// </remarks>
  public class SqlDataProvider : IDataProvider
  {
    private readonly IDbConnection _connection;

    public SqlDataProvider(IDbConnection connection)
    {
      _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public bool CanProvide(DataType dataType)
    {
      return dataType == DataType.Sql;
    }

    public object GetValue(ColumnConfig columnConfig, IDictionary<string, object> obj, Name.Gender? gender)
    {
      if (columnConfig.SqlValue == null || string.IsNullOrWhiteSpace(columnConfig.SqlValue.Query))
      {
        throw new InvalidOperationException($"Column '{columnConfig.Name}' is of type Sql but has no 'sqlValue.query' configured.");
      }

      if (_connection.State == ConnectionState.Closed)
      {
        _connection.Open();
      }

      IDictionary<string, object> safeObj = Utils.Utils.MakeParamNamesSafe(obj);
      DynamicParameters dynamicParameters = new DynamicParameters(safeObj);
      object newValue = _connection.ExecuteScalar(columnConfig.SqlValue.Query, dynamicParameters);
      if (newValue == null && columnConfig.SqlValue.ValueHandling == NotFoundValueHandling.KeepValue)
      {
        newValue = obj[columnConfig.Name];
      }

      return newValue;
    }
  }
}
