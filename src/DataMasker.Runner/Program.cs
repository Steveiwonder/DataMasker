using System;
using CommandLine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataMasker.Interfaces;
using DataMasker.Models;
using Konsole;
using Newtonsoft.Json;

namespace DataMasker.Runner
{
    internal class Program
    {
        private static readonly Dictionary<ProgressType, ProgressbarUpdate> _progressBars = new Dictionary<ProgressType, ProgressbarUpdate>();

        private static Options cliOptions;

        private static void Main(
            string[] args)
        {
            Debugger.Launch();
            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(
                       options =>
                       {
                           cliOptions = options;
                           try
                           {
                               RuntimeArgumentHandle();
                           }
                           catch (Exception ex)
                           {
                               WriteLine(ex.Message);
                           }
                       });
        }

        private static void InitProgressBars()
        {
            if (cliOptions.NoOutput)
            {
                return;
            }

            _progressBars.Add(
                ProgressType.Overall,
                new ProgressbarUpdate { ProgressBar = new ProgressBar(PbStyle.SingleLine, 0), LastMessage = "Overall Progress" });

            _progressBars.Add(
                ProgressType.Updating,
                new ProgressbarUpdate { ProgressBar = new ProgressBar(PbStyle.SingleLine, 0), LastMessage = "Update Progress" });

            _progressBars.Add(
                ProgressType.Masking,
                new ProgressbarUpdate { ProgressBar = new ProgressBar(PbStyle.SingleLine, 0), LastMessage = "Masking Progress" });
        }

        private static void UpdateProgress(
            ProgressType progressType,
            int current,
            int? max = null,
            string message = null)
        {
            if (cliOptions.NoOutput)
            {
                return;
            }

            max = max ??
                  _progressBars[progressType]
                     .ProgressBar.Max;

            _progressBars[progressType]
               .ProgressBar.Max = max.Value;

            message = message ??
                      _progressBars[progressType]
                         .LastMessage;

            _progressBars[progressType]
               .ProgressBar.Refresh(current, message);
        }
        private static void RuntimeArgumentHandle()
        {
            if (cliOptions.PrintOptions)
            {
                WriteLine();
                WriteLine(JsonConvert.SerializeObject(cliOptions, Formatting.Indented));
                WriteLine();
                return;
            }

            InitProgressBars();
            Config config = Config.Load(cliOptions.ConfigFile);
            if (cliOptions.DryRun != null)
            {
                config.DataSource.DryRun = cliOptions.DryRun.Value;
            }

            if (string.IsNullOrEmpty(cliOptions.Locale))
            {
                config.DataGeneration.Locale = cliOptions.Locale;
            }

            if (cliOptions.UpdateBatchSize != null)
            {
                config.DataSource.UpdateBatchSize = cliOptions.UpdateBatchSize;
            }

            Execute(config);
        }

        private static void WriteLine(
            string message = null)
        {
            if (!cliOptions.NoOutput)
            {
                Console.WriteLine(message);
            }
        }

        private static void Execute(
            Config config)
        {
            WriteLine("Masking Data");
            UpdateProgress(ProgressType.Overall, 0, config.Tables.Count, "Overall Progress");

            //create a data masker
            IDataMasker dataMasker = new DataMasker(new DataGenerator(config.DataGeneration));

            //grab our dataSource from the config, note: you could just ignore the config.DataSource.Type
            //and initialize your own instance
            IDataSource dataSource = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);

            for (int i = 0; i < config.Tables.Count; i++)
            {
                TableConfig tableConfig = config.Tables[i];


                //load the data, this needs optimizing for large tables
                IEnumerable<IDictionary<string, object>> rows = dataSource.GetData(tableConfig);
                UpdateProgress(ProgressType.Masking, 0, rows.Count(), "Masking Progress");
                UpdateProgress(ProgressType.Updating, 0, rows.Count(), "Update Progress");
                int rowIndex = 0;
                foreach (IDictionary<string, object> row in rows)
                {
                    //mask each row
                    dataMasker.Mask(row, tableConfig);
                    rowIndex++;

                    //update per row, or see below,
                    //dataSource.UpdateRow(row, tableConfig);
                    UpdateProgress(ProgressType.Masking, rowIndex);
                }


                //update all rows
                dataSource.UpdateRows(rows, tableConfig, totalUpdated => UpdateProgress(ProgressType.Updating, totalUpdated));
                UpdateProgress(ProgressType.Overall, i + 1);
            }

            WriteLine("Done");
        }
    }
}
