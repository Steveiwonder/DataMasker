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
    private readonly IDataProvider[] _dataProviders;

    private readonly ConcurrentDictionary<DataType, IDataProvider> _providerCache = new ConcurrentDictionary<DataType, IDataProvider>();

    /// <summary>
    /// Guards generation and mapping state, including the shared Bogus provider.
    /// </summary>
    private readonly object _uniqueLock = new object();

    /// <summary>
    /// Reserved values indexed by schema, table and column.
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<object>> _uniqueValues = new ConcurrentDictionary<string, HashSet<object>>();

    /// <summary>
    /// Original-to-generated mappings shared by unique and ordinary columns.
    /// Local mappings are scoped to schema/table/column; global mappings to column name.
    /// </summary>
    private readonly ConcurrentDictionary<string, Dictionary<object, object>> _valueMappings = new ConcurrentDictionary<string, Dictionary<object, object>>();

    /// <summary>
    /// Copies with provider mappings switched off; this masker owns mapping consistency.
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
      _dataProviders = dataProviders?.ToArray() ?? throw new ArgumentNullException(nameof(dataProviders));
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

      SeedUniqueValues(new TableConfig { Name = tableName }, columnName, values);
    }

    private void SeedUniqueValues(TableConfig table, string columnName, IEnumerable<object> values)
    {
      HashSet<object> uniqueValues = GetUniqueValueSet(TableKey(table, columnName));
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
        if (dataSource is IUniqueValueComparerSource comparerSource)
        {
          string key = TableKey(tableConfig, columnConfig.Name);
          var comparer = comparerSource.GetValueComparer(tableConfig, columnConfig.Name);
          _uniqueValues.GetOrAdd(key, _ => new HashSet<object>(comparer));
        }
        SeedUniqueValues(tableConfig, columnConfig.Name, dataSource.GetDistinctValues(tableConfig, columnConfig.Name));
      }
    }

    /// <summary>Validates masking options before any rows are changed.</summary>
    public static void ValidateTable(TableConfig tableConfig)
    {
      var active = tableConfig.Columns.Where(x => !x.Ignore).ToArray();
      for (int index = 0; index < active.Length; index++)
      {
        var column = active[index];
        if (column.Unique && (column.Type == DataType.Computed || column.Type == DataType.Sql))
          throw new InvalidOperationException($"Unique is not supported for {column.Type} column '{column.Name}'. Remove Unique or use a generated column type.");
        if (column.Type != DataType.Computed || column.SourceColumns == null) continue;
        foreach (var sourceName in column.SourceColumns)
        {
          int sourceIndex = Array.FindIndex(active, c => c.Name == sourceName);
          if (sourceIndex < 0) continue; // A direct library caller may supply unconfigured row-only inputs.
          var source = active[sourceIndex];
          if (source.Type == DataType.Sql || (source.Type == DataType.Computed && sourceIndex >= index))
            throw new InvalidOperationException($"Computed column '{column.Name}' references '{sourceName}' before it is masked. SQL sources and self/later computed sources are not supported; put computed dependencies first.");
        }
      }
    }

    private static ColumnPlan BuildColumnPlan(
        TableConfig tableConfig)
    {
      ColumnConfig[] active = tableConfig.Columns.Where(x => !x.Ignore).ToArray();
      ValidateTable(tableConfig);
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

        object newValue = GetMappedValue(tableConfig, columnConfig, obj, gender);

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

    // Length-prefix each component so identifiers containing dots cannot alias another table.
    private static string Component(string value) => $"{value.Length}:{value}";

    private static string TableKey(TableConfig table, string column) =>
        Component(string.IsNullOrWhiteSpace(table.Schema) ? "dbo" : table.Schema) + Component(table.Name) + Component(column);

    private HashSet<object> GetUniqueValueSet(string key) =>
        _uniqueValues.GetOrAdd(key, _ => new HashSet<object>());

    // A reservation belongs to an original value. Repeated references to that original may
    // reuse it, but a different original must never acquire the same unique replacement.
    private readonly Dictionary<string, Dictionary<object, object>> _reservationOwners = new Dictionary<string, Dictionary<object, object>>();
    private static readonly object NullOriginal = new object();
    private readonly Dictionary<string, HashSet<object>> _mappingOutputs = new Dictionary<string, HashSet<object>>();

    private object GetMappedValue(
        TableConfig table,
        ColumnConfig column,
        IDictionary<string, object> obj,
        Name.Gender? gender)
    {
      object original = obj[column.Name];
      bool usesMappings = column.UseGlobalValueMappings || column.UseLocalValueMappings;
      IDataProvider provider = GetDataProvider(column.Type);
      if (!column.Unique && !usesMappings)
      {
        lock (_uniqueLock) return provider.GetValue(column, obj, gender);
      }
      string key = TableKey(table, column.Name);
      bool mapOriginal = usesMappings && original != null;
      string mappingKey = column.UseGlobalValueMappings ? "global:" + Component(column.Name) : "local:" + key;

      lock (_uniqueLock)
      {
        // Preserve the provider's explicit override/retention precedence for ordinary
        // columns. These settings intentionally bypass mappings; different constants or
        // lists on related tables therefore do not promise a shared replacement.
        if (!column.Unique && (column.UseValue != null ||
            (column.UseList != null && column.UseList.Length > 0) ||
            (column.RetainNullValues && original == null) ||
            (column.RetainEmptyStringValues && original is string text && string.IsNullOrWhiteSpace(text))))
          return provider.GetValue(column, obj, gender);

        Dictionary<object, object> mappings = mapOriginal
            ? _valueMappings.GetOrAdd(mappingKey, _ => new Dictionary<object, object>()) : null;
        HashSet<object> used = column.Unique ? GetUniqueValueSet(key) : null;
        Dictionary<object, object> owners = null;
        if (column.Unique && !_reservationOwners.TryGetValue(key, out owners))
        {
          owners = new Dictionary<object, object>(new NullableValueComparer(used.Comparer));
          _reservationOwners.Add(key, owners);
        }

        HashSet<object> mappedOutputs = null;
        if (mappings != null && column.UseGlobalValueMappings && !_mappingOutputs.TryGetValue(mappingKey, out mappedOutputs))
        {
          mappedOutputs = new HashSet<object>();
          _mappingOutputs.Add(mappingKey, mappedOutputs);
        }
        if (mappings != null && mappings.TryGetValue(original, out object mapped))
        {
          Reserve(mapped, original, key, used, owners);
          return mapped;
        }

        // Explicit mappings remain authoritative, but must satisfy uniqueness too.
        if (mapOriginal && column.UseLocalValueMappings && column.ValueMappings != null &&
            column.ValueMappings.TryGetValue(original, out object configured))
        {
          Reserve(configured, original, key, used, owners);
          mappings[original] = configured;
          mappedOutputs?.Add(configured);
          return configured;
        }

        ColumnConfig generation = usesMappings
            ? _unmappedColumnConfigs.GetOrAdd(column, c => c.WithoutValueMappings()) : column;
        for (int attempt = 0; attempt < MAX_UNIQUE_VALUE_ITERATIONS; attempt++)
        {
          object value = provider.GetValue(generation, obj, gender);
          // Existing global mappings also reserve their outputs across tables. This prevents
          // new unique originals from colliding before those references reach the same table.
          bool globalCollision = column.Unique && column.UseGlobalValueMappings && mappings != null && mappedOutputs.Contains(value);
          if (column.Unique && (used.Contains(value) || globalCollision)) continue;
          Reserve(value, original, key, used, owners);
          if (mappings != null) { mappings[original] = value; mappedOutputs?.Add(value); }
          return value;
        }
        throw new InvalidOperationException($"Unable to generate unique value for {table.Name}.{column.Name}, attempted {MAX_UNIQUE_VALUE_ITERATIONS} times");
      }
    }

    private sealed class NullableValueComparer : IEqualityComparer<object>
    {
      private readonly IEqualityComparer<object> _inner;
      public NullableValueComparer(IEqualityComparer<object> inner) { _inner = inner; }
      public new bool Equals(object x, object y) =>
          ReferenceEquals(x, NullOriginal) || ReferenceEquals(y, NullOriginal)
              ? ReferenceEquals(x, y) : _inner.Equals(x, y);
      public int GetHashCode(object value) => ReferenceEquals(value, NullOriginal) ? 0 : _inner.GetHashCode(value);
    }

    private static void Reserve(object value, object original, string key, HashSet<object> used, Dictionary<object, object> owners)
    {
      if (used == null) return;
      object reservationKey = value ?? NullOriginal;
      object owner = original ?? NullOriginal;
      if (used.Contains(value) && (!owners.TryGetValue(reservationKey, out object previous) || !Equals(previous, owner)))
        throw new InvalidOperationException($"Mapped value for unique column '{key}' conflicts with an existing or previously generated value.");
      used.Add(value);
      owners[reservationKey] = owner;
    }

    private IDataProvider GetDataProvider(
        DataType dataType)
    {
      return _providerCache.GetOrAdd(dataType, type => _dataProviders.FirstOrDefault(x => x.CanProvide(type))
          ?? throw new InvalidOperationException($"No data provider registered for data type '{type}'."));
    }
  }
}
