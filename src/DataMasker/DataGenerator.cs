using System;
using System.Collections.Generic;
using Bogus;
using Bogus.DataSets;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="DataMasker.Interfaces.IDataGenerator"/>
    public class DataGenerator : IDataGenerator
    {
        private static readonly DateTime DEFAULT_MIN_DATE = new DateTime(1900, 1, 1, 0, 0, 0, 0);

        private static readonly DateTime DEFAULT_MAX_DATE = DateTime.Now;

        private const int DEFAULT_LOREM_MIN = 5;

        private const int DEFAULT_LOREM_MAX = 30;

        private const int DEFAULT_RANT_MAX = 25;

        /// <summary>
        /// The data generation configuration
        /// </summary>
        private readonly DataGenerationConfig _dataGenerationConfig;

        /// <summary>
        /// The faker
        /// </summary>
        private readonly Faker _faker;

        /// <summary>
        /// The randomizer
        /// </summary>
        private readonly Randomizer _randomizer;


        /// <summary>
        /// The global value mappings
        /// </summary>
        private readonly IDictionary<string, IDictionary<object, object>> _globalValueMappings;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataGenerator"/> class.
        /// </summary>
        /// <param name="dataGenerationConfig">The data generation configuration.</param>
        public DataGenerator(
            DataGenerationConfig dataGenerationConfig)
        {
            _dataGenerationConfig = dataGenerationConfig;
            _faker = new Faker(dataGenerationConfig.Locale ?? "en");
            _randomizer = new Randomizer();
            _globalValueMappings = new Dictionary<string, IDictionary<object, object>>();
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="columnConfig">The column configuration.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <param name="gender">The gender.</param>
        /// <returns></returns>
        public object GetValue(
            ColumnConfig columnConfig,
            object existingValue,
            Name.Gender? gender)
        {
            if (columnConfig.ValueMappings == null)
            {
                columnConfig.ValueMappings = new Dictionary<object, object>();
            }

            if (!string.IsNullOrEmpty(columnConfig.UseValue))
            {
                return ConvertValue(columnConfig.Type, columnConfig.UseValue);
            }


            if (columnConfig.RetainNullValues &&
                existingValue == null)
            {
                return null;
            }

            if (existingValue == null)
            {
                return GetValue(columnConfig, gender);
            }

            if (HasValueMapping(columnConfig, existingValue))
            {
                return GetValueMapping(columnConfig, existingValue);
            }


            object newValue = GetValue(columnConfig, gender);
            if (columnConfig.UseGlobalValueMappings ||
                columnConfig.UseLocalValueMappings)
            {
                AddValueMapping(columnConfig, existingValue, newValue);
            }

            return newValue;
        }

        /// <summary>
        /// Determines whether [has value mapping] [the specified column configuration].
        /// </summary>
        /// <param name="columnConfig">The column configuration.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <returns>
        /// <c>true</c> if [has value mapping] [the specified column configuration]; otherwise, <c>false</c>.
        /// </returns>
        private bool HasValueMapping(
            ColumnConfig columnConfig,
            object existingValue)
        {
            if (columnConfig.UseGlobalValueMappings)
            {
                return _globalValueMappings.ContainsKey(columnConfig.Name) &&
                       _globalValueMappings[columnConfig.Name]
                          .ContainsKey(existingValue);
            }

            return columnConfig.UseLocalValueMappings && columnConfig.ValueMappings.ContainsKey(existingValue);
        }

        /// <summary>
        /// Gets the value mapping.
        /// </summary>
        /// <param name="columnConfig">The column configuration.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <returns></returns>
        private object GetValueMapping(
            ColumnConfig columnConfig,
            object existingValue)
        {
            if (columnConfig.UseGlobalValueMappings)
            {
                return _globalValueMappings[columnConfig.Name][existingValue];
            }

            return columnConfig.ValueMappings[existingValue];
        }

        /// <summary>
        /// Adds the value mapping.
        /// </summary>
        /// <param name="columnConfig">The column configuration.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <param name="newValue">The new value.</param>
        private void AddValueMapping(
            ColumnConfig columnConfig,
            object existingValue,
            object newValue)
        {
            if (columnConfig.UseGlobalValueMappings)
            {
                if (_globalValueMappings.ContainsKey(columnConfig.Name))
                {
                    _globalValueMappings[columnConfig.Name]
                       .Add(existingValue, newValue);
                }
                else
                {
                    _globalValueMappings.Add(columnConfig.Name, new Dictionary<object, object> {{existingValue, newValue}});
                }
            }
            else if (columnConfig.UseLocalValueMappings)
            {
                columnConfig.ValueMappings.Add(existingValue, newValue);
            }
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="columnConfig">The column configuration.</param>
        /// <param name="gender">The gender.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Type - null</exception>
        private object GetValue(
            ColumnConfig columnConfig,
            Name.Gender? gender = null)
        {
            switch (columnConfig.Type)
            {
                case DataType.FirstName:
                    return _faker.Name.FirstName(gender);
                case DataType.LastName:
                    return _faker.Name.LastName(gender);
                case DataType.DateOfBirth:
                    return _faker.Date.Between(
                        ParseMinMaxValue(columnConfig, MinMax.Min, DEFAULT_MIN_DATE),
                        ParseMinMaxValue(columnConfig, MinMax.Max, DEFAULT_MAX_DATE));
                case DataType.Rant:
                    return _faker.Rant.Reviews(lines: ParseMinMaxValue(columnConfig, MinMax.Max, DEFAULT_RANT_MAX));
                case DataType.Lorem:
                    return _faker.Lorem.Sentence(
                        ParseMinMaxValue(columnConfig, MinMax.Min, DEFAULT_LOREM_MIN),
                        ParseMinMaxValue(columnConfig, MinMax.Max, DEFAULT_LOREM_MAX));
                case DataType.StringFormat:
                    return _randomizer.Replace(columnConfig.StringFormatPattern);
                case DataType.FullAddress:
                    return _faker.Address.FullAddress();
                case DataType.PhoneNumber:
                    return _faker.Phone.PhoneNumber(columnConfig.StringFormatPattern);
                case DataType.Bogus:
                    return _faker.Parse(columnConfig.StringFormatPattern);
            }


            throw new ArgumentOutOfRangeException(nameof(columnConfig.Type), columnConfig.Type, null);
        }

        /// <summary>
        /// Converts the value.
        /// </summary>
        /// <param name="dataType">Type of the data.</param>
        /// <param name="val">The value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">dataType - null</exception>
        private object ConvertValue(
            DataType dataType,
            string val)
        {
            switch (dataType)
            {
                case DataType.FirstName:
                case DataType.LastName:
                case DataType.Rant:
                case DataType.Lorem:
                case DataType.StringFormat:
                case DataType.FullAddress:
                case DataType.PhoneNumber:
                case DataType.None:
                    return val;
                case DataType.DateOfBirth:
                    return DateTime.Parse(val);
            }

            throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }

        private dynamic ParseMinMaxValue(
            ColumnConfig columnConfig,
            MinMax minMax,
            dynamic defaultValue = null)
        {
            string unparsedValue = minMax == MinMax.Max ? columnConfig.Max : columnConfig.Min;
            if (string.IsNullOrEmpty(unparsedValue))
            {
                return defaultValue;
            }

            switch (columnConfig.Type)
            {
                case DataType.Rant:
                case DataType.Lorem:
                    return int.Parse(unparsedValue);

                case DataType.DateOfBirth:
                    return DateTime.Parse(unparsedValue);
            }

            throw new ArgumentOutOfRangeException(nameof(columnConfig.Type), columnConfig.Type, null);
        }

        private enum MinMax
        {
            Min = 0,

            Max = 1
        }
    }
}
