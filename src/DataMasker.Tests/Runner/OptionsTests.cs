using CommandLine;
using DataMasker.Runner;

namespace DataMasker.Tests.Runner
{
    [TestClass]
    public class OptionsTests
    {
        private static Options Parse(params string[] args)
        {
            Options? parsed = null;
            new Parser(with => with.HelpWriter = null)
                .ParseArguments<Options>(args)
                .WithParsed(o => parsed = o);

            Assert.IsNotNull(parsed, $"Expected arguments to parse: {string.Join(" ", args)}");
            return parsed;
        }

        [TestMethod]
        public void DryRun_FlagWithoutValue_IsTrue()
        {
            var options = Parse("-c", "config.json", "--dry-run");

            Assert.IsTrue(options.DryRun);
        }

        [TestMethod]
        public void DryRun_ShortFlagWithoutValue_IsTrue()
        {
            var options = Parse("-c", "config.json", "-d");

            Assert.IsTrue(options.DryRun);
        }

        [TestMethod]
        public void DryRun_NotSupplied_IsFalse()
        {
            var options = Parse("-c", "config.json");

            Assert.IsFalse(options.DryRun);
        }

        [TestMethod]
        public void OtherSwitches_WithoutValue_AreTrue()
        {
            var options = Parse("-c", "config.json", "--print-options", "--no-output");

            Assert.IsTrue(options.PrintOptions);
            Assert.IsTrue(options.NoOutput);
        }
    }
}
