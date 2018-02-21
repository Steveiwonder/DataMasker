using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataMasker.DataSources;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker
{
    public static class DataSourceProvider
    {
        public static IDataSource Provide(
            DataSourceType dataSourceType, DataSourceConfig dataSourceConfig = null)
        {
            switch (dataSourceType)
            {
                case DataSourceType.InMemoryFake:
                    return new InMemoryFakeDataSource();
                case DataSourceType.SqlServer:
                    return new SqlDataSource(dataSourceConfig);

            }

            throw new ArgumentOutOfRangeException(nameof(dataSourceType), dataSourceType, null);
        }
    }
}
