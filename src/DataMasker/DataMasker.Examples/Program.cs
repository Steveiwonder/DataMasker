using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataMasker.DataSources;
using DataMasker.Interfaces;
using DataMasker.Models;
using Newtonsoft.Json;

namespace DataMasker.Examples
{
    internal class Program
    {
        private static void Main(
            string[] args)
        {
            Example1();
        }

        private static Config LoadConfig(int example)
        {
            return Config.Load($"example-configs\\config-example{example}.json");
        }

        public static void Example1()
        {
            Config config = LoadConfig(1);
            IDataMasker dataMasker = new DataMasker(new DataGenerator(config.DataGeneration));
            IDataSource dataSource = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);

            foreach (TableConfig tableConfig in config.Tables)
            {
                IEnumerable<IDictionary<string, object>> rows = dataSource.GetData(tableConfig);
                foreach (IDictionary<string, object> row in rows)
                {
                    dataMasker.Mask(row, tableConfig);
                    //update per row
                    //dataSource.UpdateRow(row, tableConfig);
                }
                //update all rows
                dataSource.UpdateRows(rows, tableConfig);
            }
        }
    }
}
