using System.Collections.Generic;
using Bogus.DataSets;
using DataMasker.Models;

namespace DataMasker.Interfaces
{
  public interface IDataProvider
  {
    bool CanProvide(DataType dataType);
    object GetValue(
        ColumnConfig columnConfig,
        IDictionary<string, object> obj,
        Name.Gender? gender);
  }
}
