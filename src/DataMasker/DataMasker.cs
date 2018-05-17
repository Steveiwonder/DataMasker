using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        /// The data generator
        /// </summary>
        private readonly IDataGenerator _dataGenerator;

        /// <summary>
        /// A dictionary key'd by {tableName}.{columnName} containing a <see cref="HashSet{T}"/> of values which have been previously used for this table/column
        /// </summary>
        private readonly ConcurrentDictionary<string, HashSet<object>> _uniqueValues = new ConcurrentDictionary<string, HashSet<object>>();
        /// <summary>
        /// Initializes a new instance of the <see cref="DataMasker"/> class.
        /// </summary>
        /// <param name="dataGenerator">The data generator.</param>
        public DataMasker(
            IDataGenerator dataGenerator)
        {
            _dataGenerator = dataGenerator;
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

            foreach (ColumnConfig columnConfig in tableConfig.Columns.Where(x => !x.Ignore))
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
                    existingValue = GetUniqueValue(tableConfig.Name, columnConfig, existingValue, gender);
                }
                else
                {
                    existingValue = _dataGenerator.GetValue(columnConfig, existingValue, gender);
                }



                //replace the original value
                obj[columnConfig.Name] = existingValue;
            }

            return obj;
        }

        private object GetUniqueValue(string tableName,
            ColumnConfig columnConfig,
            object existingValue,
            Name.Gender? gender)
        {
            //create a unique key
            string uniqueCacheKey = $"{tableName}.{columnConfig.Name}";

            //if this table/column combination hasn't been seen before add an empty hash set
            if (!_uniqueValues.ContainsKey(uniqueCacheKey))
            {
                _uniqueValues.AddOrUpdate(uniqueCacheKey, new HashSet<object>(), (a, b) => b);
            }
            //grab the hash set for this table/column 
            HashSet<object> uniqueValues = _uniqueValues[uniqueCacheKey];

            int totalIterations = 0;
            do
            {

                existingValue = _dataGenerator.GetValue(columnConfig, existingValue, gender);
                totalIterations++;
                if (totalIterations >= MAX_UNIQUE_VALUE_ITERATIONS)
                {
                    throw new Exception($"Unable to generate unique value for {uniqueCacheKey}, attempt to resolve value {totalIterations} times");
                }
            }
            while (uniqueValues.Contains(existingValue));

            uniqueValues.Add(existingValue);
            return existingValue;
        }


    }
}
