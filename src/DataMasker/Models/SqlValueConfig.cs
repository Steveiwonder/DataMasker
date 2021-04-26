
namespace DataMasker.Models
{
  public class SqlValueConfig
  {
    /// <summary>
    /// SQL Query, the first column of the first row will be selected
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Used if the <see cref="Query" /> doesn't return any data
    /// </summary>
    public NotFoundValueHandling ValueHandling { get; set; }
  }

  public enum NotFoundValueHandling
  {
    /// <summary>
    /// Keep the original value
    /// </summary>
    KeepValue,
    /// <summary>
    /// Use null
    /// </summary>
    Null
  }
}