using System;
using System.Collections.Generic;
using System.Linq;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker.DataSources
{
    /// <summary>
    /// A small in-memory data source with a fixed "Users" table, handy for trying the library out
    /// without a database.
    /// </summary>
    public class InMemoryFakeDataSource : IDataSource
    {
        private readonly IDictionary<string, IList<IDictionary<string, object>>> _tables;

        public InMemoryFakeDataSource()
        {
            _tables = CreateSeedData();
        }

        private static IDictionary<string, IList<IDictionary<string, object>>> CreateSeedData()
            => new Dictionary<string, IList<IDictionary<string, object>>>
            {
                {
                    "Users",
                    new List<IDictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            {"UserId", 1},
                            {"FirstName", "Steve"},
                            {"LastName", "Smith"},
                            {"Password", "SecurePassword!!!11"},
                            {"DOB", DateTime.Parse("1974-09-23")},
                            {"Gender", "M"},
                            {"Address", "55 Blue Street, Blue Town, Blueberry, BLue Kingdom, Blue Universe"},
                            {"ContactNumber", "+1 555-555-555-555-555-55-55"}
                        },
                        new Dictionary<string, object>
                        {
                            {"UserId", 2},
                            {"FirstName", "John "},
                            {"LastName", "Lucas"},
                            {"Password", "123SecurePassword!!!11"},
                            {"DOB", DateTime.Parse("1972-04-13")},
                            {"Gender", "M"},
                            {"Address", "56 Blue Street, Blue Town, Blueberry, BLue Kingdom, Blue Universe"},
                            {"ContactNumber", "+1 555-555-555-555-555-55-56"}
                        },
                        new Dictionary<string, object>
                        {
                            {"UserId", 3},
                            {"FirstName", "Jane"},
                            {"LastName", "Smith"},
                            {"Password", "Se123cureP33assword!!!11"},
                            {"DOB", DateTime.Parse("1938-03-21")},
                            {"Gender", "F"},
                            {"Address", "57 Blue Street, Blue Town, Blueberry, BLue Kingdom, Blue Universe"},
                            {"ContactNumber", "+1 555-555-555-555-555-55-57"}
                        }
                    }
                }
            };

        private IList<IDictionary<string, object>> GetTable(
            TableConfig tableConfig)
        {
            if (!_tables.TryGetValue(tableConfig.Name, out IList<IDictionary<string, object>> rows))
            {
                throw new InvalidOperationException($"Table '{tableConfig.Name}' does not exist in the in-memory data source. Only 'Users' is available.");
            }
            return rows;
        }

        /// <inheritdoc/>
        public IEnumerable<IDictionary<string, object>> GetData(
            TableConfig tableConfig)
        {
            return GetTable(tableConfig);
        }

        /// <inheritdoc/>
        public void UpdateRow(
            IDictionary<string, object> row,
            TableConfig tableConfig)
        {
            IList<IDictionary<string, object>> rows = GetTable(tableConfig);
            string primaryKey = tableConfig.PrimaryKeyColumn;

            if (!row.TryGetValue(primaryKey, out object key))
            {
                throw new InvalidOperationException($"Row does not contain the primary key column '{primaryKey}'.");
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (Equals(rows[i][primaryKey], key))
                {
                    rows[i] = row;
                    return;
                }
            }

            throw new InvalidOperationException($"No row with {primaryKey} = '{key}' exists in table '{tableConfig.Name}'.");
        }

        /// <inheritdoc/>
        public void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            int rowCount,
            TableConfig config,
            Action<int> updatedCallback)
        {
            int totalUpdated = 0;
            foreach (IDictionary<string, object> row in rows.ToList())
            {
                UpdateRow(row, config);
                totalUpdated++;
                updatedCallback?.Invoke(totalUpdated);
            }
        }

        /// <inheritdoc/>
        public int GetCount(TableConfig config)
        {
            return _tables.TryGetValue(config.Name, out var rows) ? rows.Count : 0;
        }

        /// <inheritdoc/>
        public IEnumerable<object> GetDistinctValues(
            TableConfig tableConfig,
            string columnName)
        {
            return GetTable(tableConfig)
                .Select(row => row.TryGetValue(columnName, out object value) ? value : null)
                .Where(value => value != null)
                .Distinct()
                .ToList();
        }
    }
}
