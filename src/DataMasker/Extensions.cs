using System.Collections.Generic;
using System.Linq;
using DataMasker.Models;

namespace DataMasker
{
    /// <summary>
    /// Extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Gets the select columns.
        /// </summary>
        /// <param name="columns">The columns.</param>
        /// <param name="primaryKeyColumn">The primary key column.</param>
        /// <returns></returns>
        public static string GetSelectColumns(
            this IList<ColumnConfig> columns,
            string primaryKeyColumn)
        {
            IList<string> columnNames = new List<string>(columns.Select(x => $"[{x.Name}]"));
            columnNames.Insert(0, primaryKeyColumn);
            return string.Join(", ", columnNames);
        }

        /// <summary>
        /// Gets the update columns.
        /// </summary>
        /// 
        /// <param name="paramPrefix">The parameter prefix.</param>
        /// <returns></returns>
        public static string GetUpdateColumns(
            this IList<ColumnConfig> columns,
            string paramPrefix = null)
        {
            return string.Join(
                               ", ",
                               columns.Where(x => !x.Ignore)
                                      .Select(x => $"[{x.Name}] = @{paramPrefix}{x.Name}"));
        }
    }
}
