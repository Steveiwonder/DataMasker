using System;
using System.Collections.Generic;
using System.Data.SqlTypes;

namespace DataMasker.DataSources
{
    // SqlString carries SQL-style linguistic comparison flags and trailing-space semantics.
    internal sealed class SqlCollationComparer : IEqualityComparer<object>
    {
        private readonly int _lcid;
        private readonly SqlCompareOptions _options;

        internal SqlCollationComparer(int lcid, int style, string collation)
        {
            _lcid = lcid;
            if (collation.IndexOf("_BIN2", StringComparison.OrdinalIgnoreCase) >= 0)
                _options = SqlCompareOptions.BinarySort2;
            else if (collation.IndexOf("_BIN", StringComparison.OrdinalIgnoreCase) >= 0)
                _options = SqlCompareOptions.BinarySort;
            else
            {
                if ((style & 1) != 0) _options |= SqlCompareOptions.IgnoreCase;
                if ((style & 2) != 0) _options |= SqlCompareOptions.IgnoreNonSpace;
                if ((style & 65536) != 0) _options |= SqlCompareOptions.IgnoreKanaType;
                if ((style & 131072) != 0) _options |= SqlCompareOptions.IgnoreWidth;
            }
        }

        private SqlString ConvertString(string value) => new SqlString(value.TrimEnd(' '), _lcid, _options);

        public new bool Equals(object x, object y)
        {
            if (x is string a && y is string b) return ConvertString(a).Equals(ConvertString(b));
            return object.Equals(x, y);
        }

        public int GetHashCode(object value) => value is string s ? ConvertString(s).GetHashCode() : value?.GetHashCode() ?? 0;
    }
}
