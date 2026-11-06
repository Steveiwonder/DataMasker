using System;
using CommandLine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DataMasker.Interfaces;
using DataMasker.Models;
using Konsole;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using DataMasker.Utils;

namespace DataMasker.Runner
{
    internal class Program
    {
        /// <summary>
        /// Minimum time between progress bar redraws. Redrawing on every row is slower than masking it.
        /// </summary>
        private static readonly TimeSpan ProgressRefreshInterval = TimeSpan.FromMilliseconds(100);

        private readonly Options _options;

        private readonly Dictionary<ProgressType, ProgressbarUpdate> _progressBars = new Dictionary<ProgressType, ProgressbarUpdate>();

        private readonly Dictionary<ProgressType, long> _lastProgressRefresh = new Dictionary<ProgressType, long>();

        private readonly Stopwatch _progressClock = Stopwatch.StartNew();

        private Program(
            Options options)
        {
            _options = options;
        }

        private static int Main(
            string[] args)
        {
            return Run(args);
        }

        /// <summary>
        /// Runs the CLI and returns the process exit code: 0 on success, 1 when masking fails,
        /// 2 when the arguments could not be parsed.
        /// </summary>
        internal static int Run(
            string[] args)
        {
            int exitCode = 0;

            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(
                       options =>
                       {
                           try
                           {
                               new Program(options).RuntimeArgumentHandle();
                           }
                           catch (Exception ex)
                           {
                               // Always report failures, even with --no-output, otherwise a CI
                               // pipeline has no way of knowing the run failed.
                               Console.Error.WriteLine($"Error: {ex.Message}");
                               exitCode = 1;
                           }
                       })
                  .WithNotParsed(
                       errors =>
                       {
                           bool informational = errors.All(e => e.Tag == ErrorType.HelpRequestedError ||
                                                                e.Tag == ErrorType.HelpVerbRequestedError ||
                                                                e.Tag == ErrorType.VersionRequestedError);
                           exitCode = informational ? 0 : 2;
                       });

            return exitCode;
        }

        private void InitProgressBars()
        {
            if (_options.NoOutput)
            {
                return;
            }

            _progressBars[ProgressType.Overall] =
                new ProgressbarUpdate { ProgressBar = new ProgressBar(PbStyle.SingleLine, 0), LastMessage = "Overall Progress" };

            _progressBars[ProgressType.Updating] =
                new ProgressbarUpdate { ProgressBar = new ProgressBar(PbStyle.SingleLine, 0), LastMessage = "Update Progress" };

            _progressBars[ProgressType.Masking] =
                new ProgressbarUpdate { ProgressBar = new ProgressBar(PbStyle.SingleLine, 0), LastMessage = "Masking Progress" };
        }

        private void UpdateProgress(
            ProgressType progressType,
            int current,
            int? max = null,
            string message = null)
        {
            if (_options.NoOutput)
            {
                return;
            }

            ProgressbarUpdate bar = _progressBars[progressType];

            max = max ?? bar.ProgressBar.Max;
            message = message ?? bar.LastMessage;

            // Only redraw when something other than the counter changed, when the bar is
            // complete, or when enough time has passed since the last redraw.
            bool structuralChange = max != bar.ProgressBar.Max || message != bar.LastMessage;
            bool complete = current >= max;
            long now = _progressClock.ElapsedMilliseconds;
            bool due = !_lastProgressRefresh.TryGetValue(progressType, out long last) || now - last >= ProgressRefreshInterval.TotalMilliseconds;

            if (!structuralChange && !complete && !due)
            {
                return;
            }

            bar.ProgressBar.Max = max.Value;
            bar.LastMessage = message;
            bar.ProgressBar.Refresh(current, message);
            _lastProgressRefresh[progressType] = now;
        }

        private void RuntimeArgumentHandle()
        {
            if (_options.PrintOptions)
            {
                WriteLine();
                WriteLine(JsonConvert.SerializeObject(_options, Formatting.Indented));
                WriteLine();
                return;
            }

            InitProgressBars();
            Config config = Config.Load(_options.ConfigFile);

            // --dry-run is a switch, so it can only ever turn dry run on. When it isn't
            // supplied we leave whatever the config file says alone.
            if (_options.DryRun)
            {
                config.DataSource.DryRun = true;
            }

            if (!string.IsNullOrEmpty(_options.Locale))
            {
                config.DataGeneration.Locale = _options.Locale;
            }

            if (_options.UpdateBatchSize != null)
            {
                config.DataSource.UpdateBatchSize = _options.UpdateBatchSize;
            }

            Execute(config);
        }

        private void WriteLine(
            string message = null)
        {
            if (!_options.NoOutput)
            {
                Console.WriteLine(message);
            }
        }

        private void Execute(
            Config config)
        {
            WriteLine("Masking Data");
            UpdateProgress(ProgressType.Overall, 0, config.Tables.Count, "Overall Progress");

            //grab our dataSource from the config, note: you could just ignore the config.DataSource.Type
            //and initialize your own instance
            IDataSource dataSource = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);

            var dataProviders = new List<IDataProvider>
            {
                new BogusDataProvider(config.DataGeneration)
            };

            // The Sql column type needs a live connection, which only makes sense for SQL Server.
            SqlConnection sqlProviderConnection = null;
            if (config.DataSource.Type == DataSourceType.SqlServer)
            {
                sqlProviderConnection = new SqlConnection(config.DataSource.GetConnectionString());
                dataProviders.Add(new SqlDataProvider(sqlProviderConnection));
            }

            using (sqlProviderConnection)
            {
                //create a data masker
                IDataMasker dataMasker = new DataMasker(dataProviders);

                for (int i = 0; i < config.Tables.Count; i++)
                {
                    TableConfig tableConfig = config.Tables[i];

                    int rowCount = dataSource.GetCount(tableConfig);
                    UpdateProgress(ProgressType.Masking, 0, rowCount, "Masking Progress");
                    UpdateProgress(ProgressType.Updating, 0, rowCount, "Update Progress");

                    // Make sure unique columns never receive a value that already exists in the table.
                    dataMasker.SeedUniqueValues(dataSource, tableConfig);

                    IEnumerable<IDictionary<string, object>> rows = dataSource.GetData(tableConfig);

                    int rowIndex = 0;
                    var maskedRows = rows.Select(row =>
                    {
                        rowIndex++;
                        UpdateProgress(ProgressType.Masking, rowIndex);
                        return dataMasker.Mask(row, tableConfig);
                    });

                    //update all rows
                    dataSource.UpdateRows(maskedRows, rowCount, tableConfig, totalUpdated => UpdateProgress(ProgressType.Updating, totalUpdated));
                    UpdateProgress(ProgressType.Overall, i + 1);
                }

                if (config.SqlStatements != null && config.SqlStatements.Count > 0)
                {
                    ISqlStatementDataSource sqlStatementDataSource = SqlStatementDataProvier.Provide(config.DataSource.Type, config.DataSource);

                    int statementCount = config.SqlStatements.Count;
                    UpdateProgress(ProgressType.Masking, 0, statementCount, "Masking Progress");
                    UpdateProgress(ProgressType.Updating, 0, statementCount, "Executing SQL statements");

                    for (int i = 0; i < statementCount; i++)
                    {
                        sqlStatementDataSource.Execute(config.SqlStatements[i]);
                        UpdateProgress(ProgressType.Updating, i + 1);
                    }
                }
            }

            WriteLine("Done");
        }
    }
}
