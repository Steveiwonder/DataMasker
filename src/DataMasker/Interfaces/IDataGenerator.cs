using Bogus.DataSets;
using DataMasker.Models;

namespace DataMasker.Interfaces
{
    public interface IDataGenerator
    {
        object GetValue(
            ColumnConfig columnConfig,
            object existingValue,
            Name.Gender? gender);
    }
}
