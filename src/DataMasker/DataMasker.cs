using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bogus.DataSets;
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
    /// Guards all unique value state. Masking is single threaded in practice (the Bogus
    /// faker is not thread safe) so one lock keeps the bookkeeping simple and correct.
    /// </summary>
    private readonly object _uniqueLock = new object();

    /// <summary>
    /// A dictionary key'd by {tableName}.{columnName} containing a <see cref="HashSet{T}"/> of values which have been previously used for this table/column
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<object>> _uniqueValues = new ConcurrentDictionary<string, HashSet<object>>();

    /// <summary>
    /// For unique columns that also use value mappings: original value -> generated value.
    /// Key'd by column name for global mappings and {tableName}.{columnName} for local ones.
    /// </summary>
    private readonly ConcurrentDictionary<string, Dictionary<object, object>> _uniqueValueMappings = new ConcurrentDictionary<string, Dictionary<object, object>>();

    /// <summary>
    /// Copies of column configs with value mappings switched off, used while searching for a
    /// unique value. Without this the provider keeps returning the same mapped value on retry.
    /// </summary>
    private readonly ConcurrentDictionary<ColumnConfig, ColumnConfig> _unmappedColumnConfigs = new ConcurrentDictionary<ColumnConfig, ColumnConfig>();

    /// <summary>
    /// Column lists split by masking phase, computed once per table instead of once per row.
    /// </summary>
    private readonly ConcurrentDictionary<TableConfig, ColumnPlan> _columnPlans = new ConcurrentDictionary<TableConfig, ColumnPlan>();

    private sealed class ColumnPlan
    {
      public ColumnConfig[] Normal;
      public ColumnConfig[] Computed;
      public ColumnConfig[] Sql;
    }

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
      ColumnPlan plan = _columnPlans.GetOrAdd(tableConfig, BuildColumnPlan);
      obj = MaskNormal(obj, tableConfig, plan.Normal);
      obj = MaskComputed(obj, tableConfig, plan.Computed);
      obj = MaskSql(obj, tableConfig, plan.Sql);
      return obj;
    }

    /// <inheritdoc/>
    public void SeedUniqueValues(
        string tableName,
        string columnName,
        IEnumerable<object> values)
    {
      if (values == null)
      {
        return;
      }

      HashSet<object> uniqueValues = GetUniqueValueSet(tableName, columnName);
      lock (_uniqueLock)
      {
        foreach (object value in values)
        {
          if (value != null)
          {
            uniqueValues.Add(value);
          }
        }
      }
    }

    /// <inheritdoc/>
    public void SeedUniqueValues(
        IDataSource dataSource,
        TableConfig tableConfig)
    {
      if (dataSource == null)
      {
        throw new ArgumentNullException(nameof(dataSource));
      }

      foreach (ColumnConfig columnConfig in tableConfig.Columns.Where(x => x.Unique && !x.Ignore))
      {
        SeedUniqueValues(tableConfig.Name, columnConfig.Name, dataSource.GetDistinctValues(tableConfig, columnConfig.Name));
      }
    }

    private static ColumnPlan BuildColumnPlan(
        TableConfig tableConfig)
    {
      ColumnConfig[] active = tableConfig.Columns.Where(x => !x.Ignore).ToArray();
      return new ColumnPlan
      {
        Normal = active.Where(x => x.Type != DataType.Computed && x.Type != DataType.Sql).ToArray(),
        Computed = active.Where(x => x.Type == DataType.Computed).ToArray(),
        Sql = active.Where(x => x.Type == DataType.Sql).ToArray()
      };
    }

    private IDictionary<string, object> MaskNormal(
        IDictionary<string, object> obj,
        TableConfig tableConfig,
        IEnumerable<ColumnConfig> columnConfigs)
    {
      foreach (ColumnConfig columnConfig in columnConfigs)
      {
        if (!obj.ContainsKey(columnConfig.Name))
        {
          throw new InvalidOperationException($"Column '{columnConfig.Name}' was not found in the row loaded for table '{tableConfig.Name}'. Check the column name in the configuration.");
        }

        Name.Gender? gender = null;
        if (!string.IsNullOrEmpty(columnConfig.UseGenderColumn))
        {
          if (!obj.TryGetValue(columnConfig.UseGenderColumn, out object g))
          {
            throw new InvalidOperationException($"Gender column '{columnConfig.UseGenderColumn}' for column '{columnConfig.Name}' was not found in the row. Add it to the table's columns (it can be marked ignore: true).");
          }
          gender = Utils.Utils.TryParseGender(g?.ToString());
        }

        object newValue;
        if (columnConfig.Unique)
        {
          newValue = GetUniqueValue(tableConfig.Name, columnConfig, obj, gender);
        }
        else
        {
          IDataProvider dataProvider = GetDataProvider(columnConfig.Type);
          newValue = dataProvider.GetValue(columnConfig, obj, gender);
        }

        //replace the original value
        obj[columnConfig.Name] = newValue;
      }
      return obj;
    }

    private IDictionary<string, object> MaskComputed(
        IDictionary<string, object> obj,
        TableConfig tableConfig,
        IEnumerable<ColumnConfig> columnConfigs)
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
        TableConfig tableConfig,
        IEnumerable<ColumnConfig> columnConfigs)
    {
      foreach (var columnConfig in columnConfigs)
      {
        IDataProvider dataProvider = GetDataProvider(DataType.Sql);
        obj[columnConfig.Name] = dataProvider.GetValue(columnConfig, obj, null);
      }
      return obj;
    }

    private HashSet<object> GetUniqueValueSet(
        string tableName,
        string columnName)
    {
      return _uniqueValues.GetOrAdd($"{tableName}.{columnName}", _ => new HashSet<object>());
    }

    private object GetUniqueValue(
        string tableName,
        ColumnConfig columnConfig,
        IDictionary<string, object> obj,
        Name.Gender? gender)
    {
      string uniqueCacheKey = $"{tableName}.{columnConfig.Name}";
      HashSet<object> uniqueValues = GetUniqueValueSet(tableName, columnConfig.Name);
      object existingValue = obj[columnConfig.Name];

      bool usesMappings = columnConfig.UseGlobalValueMappings || columnConfig.UseLocalValueMappings;
      string mappingKey = columnConfig.UseGlobalValueMappings ? columnConfig.Name : uniqueCacheKey;

      lock (_uniqueLock)
      {
        Dictionary<object, object> mappings = null;
        if (usesMappings && existingValue != null)
        {
          // A value we've already replaced in this run always maps to the same replacement.
          mappings = _uniqueValueMappings.GetOrAdd(mappingKey, _ => new Dictionary<object, object>());
          if (mappings.TryGetValue(existingValue, out object mapped))
          {
            return mapped;
          }

          // Mappings supplied in the configuration are honoured as-is.
          if (columnConfig.UseLocalValueMappings &&
              columnConfig.ValueMappings != null &&
              columnConfig.ValueMappings.TryGetValue(existingValue, out object configured))
          {
            uniqueValues.Add(configured);
            mappings[existingValue] = configured;
            return configured;
          }
        }

        // Generate with mappings switched off, otherwise the provider would hand back the
        // same mapped value on every retry and we could never find a unique one.
        ColumnConfig generationConfig = usesMappings
            ? _unmappedColumnConfigs.GetOrAdd(columnConfig, c => c.WithoutValueMappings())
            : columnConfig;
        IDataProvider dataProvider = GetDataProvider(columnConfig.Type);

        object newValue;
        int totalIterations = 0;
        do
        {
          newValue = dataProvider.GetValue(generationConfig, obj, gender);
          totalIterations++;
          if (totalIterations >= MAX_UNIQUE_VALUE_ITERATIONS)
          {
            throw new InvalidOperationException($"Unable to generate unique value for {uniqueCacheKey}, attempted {totalIterations} times");
          }
        }
        while (uniqueValues.Contains(newValue));

        uniqueValues.Add(newValue);
        if (mappings != null)
        {
          mappings[existingValue] = newValue;
        }
        return newValue;
      }
    }

    private IDataProvider GetDataProvider(
        DataType dataType)
    {
      return _dataProviders.FirstOrDefault(x => x.CanProvide(dataType))
          ?? throw new InvalidOperationException($"No data provider registered for data type '{dataType}'.");
    }
  }
}
