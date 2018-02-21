using System;
using CommandLine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataMasker.Interfaces;
using DataMasker.Models;
using Newtonsoft.Json;

namespace DataMasker.Runner
{
    internal class Program
    {
        private static void Main(
            string[] args)
        {
            //Debugger.Launch();
            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(
                       options => {
                           try
                           {
                               RuntimeArgumentHandle(options);
                           }
                           catch (Exception ex)
                           {
                               Console.WriteLine(ex.Message);
                           }
                       });
        }

        private static void RuntimeArgumentHandle(
            Options options)
        {
            if (options.PrintOptions)
            {
                Console.WriteLine();
                Console.WriteLine(JsonConvert.SerializeObject(options, Formatting.Indented));
                Console.WriteLine();
                return;
            }

            Config config = Config.Load(options.ConfigFile);
            if (options.DryRun != null)
            {
                config.DataSource.DryRun = options.DryRun.Value;
            }

            if (string.IsNullOrEmpty(options.Locale))
            {
                config.DataGeneration.Locale = options.Locale;
            }

            if (options.UpdateBatchSize != null)
            {
                config.DataSource.UpdateBatchSize = options.UpdateBatchSize;
            }

            Execute(config);
        }

        private static void Execute(
            Config config)
        {
            //create a data masker
            IDataMasker dataMasker = new DataMasker(new DataGenerator(config.DataGeneration));

            //grab our dataSource from the config, note: you could just ignore the config.DataSource.Type
            //and initialize your own instance
            IDataSource dataSource = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);

            //Enumerable all our tables and being masking the data
            foreach (TableConfig tableConfig in config.Tables)
            {
                Console.WriteLine($"Masking data for table {tableConfig.Name}");

                //load the data, this needs optimizing for large tables
                IEnumerable<IDictionary<string, object>> rows = dataSource.GetData(tableConfig);
                foreach (IDictionary<string, object> row in rows)
                {
                    //mask each row
                    dataMasker.Mask(row, tableConfig);

                    //update per row, or see below,
                    //dataSource.UpdateRow(row, tableConfig);
                }

                Console.WriteLine($"Finished masking data for table {tableConfig.Name}");
                Console.WriteLine($"Updating rows for table {tableConfig.Name}");

                //update all rows, in batches of 100
                dataSource.UpdateRows(rows, tableConfig);
            }
        }
    }
}
