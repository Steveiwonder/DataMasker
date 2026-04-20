using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bogus.DataSets;
using DataMasker.DataSources;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker
{
  /// <summary>
  /// DataMasker
  /// </summary>
  /// <seealso cref="DataMasker.Interfaces.IDataMasker"/>
  public class DataMasker : IDataMasker
  {
    /// <summary>
    /// The maximum iterations allowed when attempting to retrieve a unique value per column
    /// </summary>
    private const int MAX_UNIQUE_VALUE_ITERATIONS = 5000;
    /// <summary>
    /// A set of available data providers
    /// </summary>
    private readonly IEnumerable<IDataProvider> _dataProviders;

    /// <summary>
    /// A dictionary key'd by {tableName}.{columnName} containing a <see cref="HashSet{T}"/> of values which have been previously used for this table/column
    /// </summary>
    private readonly ConcurrentDictionary<string, object> _uniqueValueLocks = new ConcurrentDictionary<string, object>();
    private readonly ConcurrentDictionary<string, HashSet<object>> _uniqueValues = new ConcurrentDictionary<string, HashSet<object>>();
    /// <summary>
    /// Initializes a new instance of the <see cref="DataMasker"/> class.
    /// </summary>
    /// <param name="dataProviders">A set of data proiders.</param>
    public DataMasker(
        IEnumerable<IDataProvider> dataProviders)
    {
      _dataProviders = dataProviders;
    }


  

        /// <summary>
        /// Masks the specified object with new data
        /// </summary>
        /// <param name="obj">The object to mask</param>
        /// <param name="tableConfig">The table configuration.</param>
        /// <returns></returns>
        public IDictionary<string, object> Mask(
        IDictionary<string, object> obj,
        TableConfig tableConfig)
    {
      obj = MaskNormal(obj, tableConfig, tableConfig.Columns.Where(x => !x.Ignore && x.Type != DataType.Computed && x.Type != DataType.Sql));
      obj = MaskComputed(obj, tableConfig, tableConfig.Columns.Where(x => !x.Ignore && x.Type == DataType.Computed && x.Type != DataType.Sql));
      obj = MaskSql(obj, tableConfig, tableConfig.Columns.Where(x => !x.Ignore && x.Type == DataType.Sql));
      return obj;
    }
    private IDictionary<string, object> MaskNormal(
              IDictionary<string, object> obj,
              TableConfig tableConfig, IEnumerable<ColumnConfig> columnConfigs)
    {

      foreach (ColumnConfig columnConfig in columnConfigs)
      {
        object existingValue = obj[columnConfig.Name];

        Name.Gender? gender = null;
        if (!string.IsNullOrEmpty(columnConfig.UseGenderColumn))
        {
          object g = obj[columnConfig.UseGenderColumn];
          gender = Utils.Utils.TryParseGender(g?.ToString());
        }

        if (columnConfig.Unique)
        {
          existingValue = GetUniqueValue(tableConfig.Name, columnConfig, obj, gender);
        }
        else
        {
          IDataProvider dataProvider = this.GetDataProvider(columnConfig.Type);
          existingValue = dataProvider.GetValue(columnConfig, obj, gender);
        }
        //replace the original value
        obj[columnConfig.Name] = existingValue;
      }
      return obj;
    }

    private IDictionary<string, object> MaskComputed(
              IDictionary<string, object> obj,
              TableConfig tableConfig, IEnumerable<ColumnConfig> columnConfigs)
    {
      foreach (ColumnConfig columnConfig in columnConfigs)
      {
        if (columnConfig.SourceColumns == null || columnConfig.SourceColumns.Length == 0)
        {
          throw new InvalidOperationException($"Column '{columnConfig.Name}' is of type Computed but has no SourceColumns configured.");
        }

        var separator = columnConfig.Separator ?? " ";
        StringBuilder colValue = new StringBuilder();
        bool first = true;
        foreach (var sourceColumn in columnConfig.SourceColumns)
        {
          if (!obj.ContainsKey(sourceColumn))
          {
            throw new InvalidOperationException($"Source column '{sourceColumn}' could not be found in the current row.");
          }

          if (first)
          {
            first = false;
          }
          else
          {
            colValue.Append(separator);
          }
          colValue.Append(obj[sourceColumn] ?? String.Empty);
        }
        obj[columnConfig.Name] = colValue.ToString();
      }
      return obj;
    }

    private IDictionary<string, object> MaskSql(
              IDictionary<string, object> obj,
              TableConfig tableConfig, IEnumerable<ColumnConfig> columnConfigs)
    {

      foreach (var columnConfig in columnConfigs)
      {
        IDataProvider dataProvider = this.GetDataProvider(DataType.Sql);
        obj[columnConfig.Name] = dataProvider.GetValue(columnConfig, obj, null);
      }
      return obj;
    }


    private object GetUniqueValue(string tableName,
        ColumnConfig columnConfig,
        IDictionary<string, object> obj,
        Name.Gender? gender)
    {
      object existingValue = obj[columnConfig.Name];
      //create a unique key
      string uniqueCacheKey = $"{tableName}.{columnConfig.Name}";

      HashSet<object> uniqueValues = _uniqueValues.GetOrAdd(uniqueCacheKey, _ => new HashSet<object>());
      object lockObj = _uniqueValueLocks.GetOrAdd(uniqueCacheKey, _ => new object());

      lock (lockObj)
      {
        int totalIterations = 0;
        do
        {
          IDataProvider dataProvider = this.GetDataProvider(columnConfig.Type);
          existingValue = dataProvider.GetValue(columnConfig, obj, gender);
          totalIterations++;
          if (totalIterations >= MAX_UNIQUE_VALUE_ITERATIONS)
          {
            throw new InvalidOperationException($"Unable to generate unique value for {uniqueCacheKey}, attempted {totalIterations} times");
          }
        }
        while (uniqueValues.Contains(existingValue));

        uniqueValues.Add(existingValue);
      }
      return existingValue;
    }


    private IDataProvider GetDataProvider(DataType dataType)
    {
      return this._dataProviders.FirstOrDefault(x => x.CanProvide(dataType))
          ?? throw new InvalidOperationException($"No data provider registered for data type '{dataType}'.");
    }

  }
}
