using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace DataMasker.Models
{
  /// <summary>
  /// Column Configuration
  /// </summary>
  public class ColumnConfig
  {
    /// <summary>
    /// The name of the column
    /// </summary>
    /// <value>
    /// The name.
    /// </value>
    [JsonRequired]
    public string Name { get; set; }


    /// <summary>
    /// The type of data contained within the column, fist name, last name, date of birth etc.
    /// </summary>
    /// <value>
    /// The type.
    /// </value>
    [JsonRequired]
    public DataType Type { get; set; }

    /// <summary>
    /// Specific value mappings
    /// <remarks>
    /// e.g. If value "Steve" is found always replace it with "John"
    /// </remarks>
    /// </summary>
    /// <value>
    /// The value mappings.
    /// </value>

    public IDictionary<object, object> ValueMappings { get; set; }

    /// <summary>
    /// When generated data for this column, take into consideration a gender column, can be null
    /// </summary>
    /// <value>
    /// The use gender column.
    /// </value>
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string UseGenderColumn { get; set; }

    /// <summary>
    /// When specifying a column that doesn't need to be masked but is needed in the select, such a gender column.
    /// You don't want to change the gender, but you need the gender data to be able to create data for other columns
    /// </summary>
    /// <value>
    /// <c>true</c> if ignore; otherwise, <c>false</c>.
    /// </value>
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool Ignore { get; set; }

    /// <summary>
    /// Only used for some data tables, but when generating lorem ipsum you specify the number of words
    /// </summary>
    /// <value>
    /// The maximum.
    /// </value>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Max { get; set; }

    /// <summary>
    /// Only used for some data tables, but when generating lorem ipsum you specify the number of words
    /// </summary>
    /// <value>
    /// The minimum.
    /// </value>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Min { get; set; }

    /// <summary>
    /// If the <see cref="Type"/> is <see cref="DataType.StringFormat"/> you can specify any a format to use when generating
    /// that data
    /// <remarks>See https://github.com/bchavez/Bogus#replace</remarks>
    /// </summary>
    /// <value>
    /// The string format pattern.
    /// </value>
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string StringFormatPattern { get; set; }

    /// <summary>
    /// When set, this value will be used for all rows
    /// </summary>
    /// <value>
    /// The use value.
    /// </value>
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string UseValue { get; set; }


    /// <summary>
    /// When true, if the data loaded from the source is null, then no new data will be generated.
    /// When false, if the data loaded from the source is null it will be replaced by new data
    /// </summary>
    /// <value>
    /// <c>true</c> if [retain null values]; otherwise, <c>false</c>.
    /// </value>
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool RetainNullValues { get; set; }

    /// <summary>
    /// When true, if the data loaded from the source is an empty string (String.IsNullOrWhitespace), then no new data will be generated.
    /// When false, if the data loaded from the source is an empty string (String.IsNullOrWhitespace) it will be replaced by new data
    /// </summary>
    /// <value>
    /// <c>true</c> if [retain empty values]; otherwise, <c>false</c>.
    /// </value>
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool RetainEmptyStringValues { get; set; }

    /// <summary>
    /// When true, mappings specific to this column will be used
    /// <remarks>
    /// If you have 10 rows of data each with column "Surname" and you will for all the people with the same surname
    /// to get a new surname but shared, this should be true.
    /// </remarks>
    /// </summary>
    /// <value>
    /// <c>true</c> if [use local value mappings]; otherwise, <c>false</c>.
    /// </value>
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool UseLocalValueMappings { get; set; }

    /// <summary>
    /// This works the same way as <see cref="UseLocalValueMappings"/> but is shared across <see cref="TableConfig"/>'s
    /// </summary>
    /// <value>
    /// <c>true</c> if [use global value mappings]; otherwise, <c>false</c>.
    /// </value>
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool UseGlobalValueMappings { get; set; }

    /// <summary>
    /// When true, any values replaced in this column will be unique
    /// </summary>
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool Unique { get; set; }

    /// <summary>
    /// Use in conjunction with <see cref="DataType.Sql" />
    /// </summary>
    public SqlValueConfig SqlValue { get; set; }


    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string Separator { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string[] SourceColumns { get; set; }
  }
}
