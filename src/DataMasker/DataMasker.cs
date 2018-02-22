using System;
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
        /// The data generator
        /// </summary>
        private readonly IDataGenerator _dataGenerator;

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
            foreach (ColumnConfig tableColumn in tableConfig.Columns.Where(x => !x.Ignore))
            {
                object existingValue = obj[tableColumn.Name];

                Name.Gender? gender = null;
                if (!string.IsNullOrEmpty(tableColumn.UseGenderColumn))
                {
                    object g = obj[tableColumn.UseGenderColumn];
                    gender = Utils.Utils.TryParseGender(g?.ToString());
                }


                existingValue = _dataGenerator.GetValue(tableColumn, existingValue, gender);

                //replace the original value
                obj[tableColumn.Name] = existingValue;
            }

            return obj;
        }


    }
}
