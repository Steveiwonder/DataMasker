using System;
using System.Collections.Generic;
using System.Linq;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker.DataSources
{
    public class InMemoryFakeDataSource : IDataSource
    {
        private readonly IDictionary<string, IList<IDictionary<string, object>>> tables;

        private IDictionary<string, IList<IDictionary<string, object>>> tableData
            => new Dictionary<string, IList<IDictionary<string, object>>>
            {
                {
                    "Users",
                    new List<IDictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            {"UserId", 1},
                            {"  FirstName", "Steve"},
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

        public InMemoryFakeDataSource()
        {
            tables = tableData;
        }

        /// <inheritdoc/>
        public IEnumerable<IDictionary<string, object>> GetData(
            TableConfig tableConfig)
        {
            return tableData[tableConfig.Name];
        }

        /// <inheritdoc/>
        public void UpdateRow(
            IDictionary<string, object> row,
            TableConfig tableConfig)
        {

            int index = tables[tableConfig.Name]
               .IndexOf(
                    tables[tableConfig.Name]
                       .Single(
                            x =>
                            {
                                bool e = x["UserId"]
                                   .Equals(row["UserId"]);

                                return e;
                            }));

            tables[tableConfig.Name][index] = row;
        }

        /// <inheritdoc/>
        public void UpdateRows(
            IEnumerable<IDictionary<string, object>> rows,
            int rowCount,
            TableConfig config,
            Action<int> updatedCallback)
        {

            foreach (IDictionary<string, object> dictionary in rows)
            {
                UpdateRow(dictionary, config);
            }
        }

        public int GetCount(TableConfig config)
        {
            return tableData.Count;
        }
    }
}
