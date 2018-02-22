using CommandLine;

namespace DataMasker.Runner
{
    public class Options
    {
        [Option('c', "config-file", Required = true, HelpText = "json config file")]
        public string ConfigFile { get; set; }

        [Option('d', "dry-run", Default = false, Required = false, HelpText = "Dry run, only supported by some data sources")]
        public bool? DryRun { get; set; }

        [Option('l', "locale", Default = null, HelpText = "Set the locale")]
        public string Locale { get; set; }

        [Option('u', "update-batchsize", Default = null, HelpText = "Batch Size to use when upating records")]
        public int? UpdateBatchSize { get; set; }


        [Option("print-options", Default = false, HelpText = "Prints the arguments passed into this tool in a json format")]
        public bool PrintOptions { get; set; }

        [Option("no-output", Default = false, HelpText = "If set, not output to the console will be written")]
        public bool NoOutput { get; set; }
    }
}
