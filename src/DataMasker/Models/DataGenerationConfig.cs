using System;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DataMasker.Models
{
    /// <summary>
    /// DataGenerationConfig
    /// </summary>
    public class DataGenerationConfig
    {
        public static readonly DataGenerationConfig Default = new DataGenerationConfig { MinDate = DEFAULT_MIN_DATE, MaxDate = DEFAULT_MAX_DATE };

        private static readonly DateTime DEFAULT_MIN_DATE = new DateTime(1900, 1, 1, 0, 0, 0, 0);

        private static readonly DateTime DEFAULT_MAX_DATE = DateTime.Now;

        private DateTime? _minDate;

        private DateTime? _maxDate;

        /// <summary>
        /// Gets or sets the minimum date to use when generating this data type
        /// </summary>
        /// <value>
        /// The minimum date.
        /// </value>

        public DateTime MinDate
        {
            get { return _minDate ?? (_minDate = DEFAULT_MIN_DATE).Value; }
            set { _minDate = value; }
        }

        /// <summary>
        /// Gets or sets the maxiumum date to use when generating this data type
        /// </summary>
        /// <value>
        /// The maxiumum date.
        /// </value>
        public DateTime MaxDate
        {
            get { return _maxDate ?? (_maxDate = DEFAULT_MAX_DATE).Value; }
            set { _maxDate = value; }
        }


        public string Locale { get; set; } = "en";
    }
}
