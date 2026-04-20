using Bogus.DataSets;
using DataMasker.Interfaces;
using DataMasker.Models;
using System.Data;
using Dapper;
using System.Collections.Generic;

namespace DataMasker
{

  public class SqlDataProvider : IDataProvider
  {
    private readonly IDbConnection _connection;

    public SqlDataProvider(IDbConnection connection)
    {
      _connection = connection;
    }
    public bool CanProvide(DataType dataType)
    {
      return dataType == DataType.Sql;
    }

    public object GetValue(ColumnConfig columnConfig, IDictionary<string, object> obj, Name.Gender? gender)
    {

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