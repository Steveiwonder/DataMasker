using CommandLine;

namespace DataMasker.Runner
{
  public class Options
  {
    [Option('c', "config-file", Required = true, HelpText = "the json configuration to be")]
    public string ConfigFile { get; set; }

    [Option('d', "dry-run", Default = false, Required = false, HelpText = "dry run, only supported by some data sources")]
    public bool? DryRun { get; set; }

    [Option('l', "locale", Default = null, HelpText = "set the locale")]
    public string Locale { get; set; }

    [Option('u', "update-batchsize", Default = null, HelpText = "batch size to use when upating records")]
    public int? UpdateBatchSize { get; set; }


    [Option("print-options", Default = false, HelpText = "prints the arguments passed into this tool in a json format without executing anything else")]
    public bool PrintOptions { get; set; }

    [Option("no-output", Default = false, HelpText = "if set, no output to the console will be written")]
    public bool NoOutput { get; set; }
  }
}
