using System.Collections.Generic;
using DataMasker.Models;

namespace DataMasker.Interfaces
{
    /// <summary>Supplies equality rules for unique values in a destination column.</summary>
    public interface IUniqueValueComparerSource
    {
        /// <summary>Returns the destination column's value comparer.</summary>
        IEqualityComparer<object> GetValueComparer(TableConfig tableConfig, string columnName);
    }
}
