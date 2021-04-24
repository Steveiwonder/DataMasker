using Bogus.DataSets;
using DataMasker.Models;

namespace DataMasker.Interfaces
{
  public interface IDataProvider
  {
    bool CanProvide(DataType dataType);
    object GetValue(
        ColumnConfig columnConfig,
        object existingValue,
        Name.Gender? gender);
  }
}
