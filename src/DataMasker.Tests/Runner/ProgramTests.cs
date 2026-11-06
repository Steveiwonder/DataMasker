using DataMasker.Runner;

namespace DataMasker.Tests.Runner
{
    [TestClass]
    [DoNotParallelize] // these tests redirect Console.Out / Console.Error, which are process wide
    public class ProgramTests
    {
        [TestMethod]
        public void Run_MissingConfigFile_ReturnsOneAndReportsOnStderr()
        {
            var stderr = new StringWriter();
            var original = Console.Error;
            Console.SetError(stderr);
            try
            {
                int exitCode = Program.Run(new[] { "-c", "does-not-exist.json", "--no-output" });

                Assert.AreEqual(1, exitCode);
                StringAssert.Contains(stderr.ToString(), "does-not-exist.json");
            }
            finally
            {
                Console.SetError(original);
            }
        }

        [TestMethod]
        public void Run_UnknownArgument_ReturnsTwo()
        {
            var stderr = new StringWriter();
            var original = Console.Error;
            Console.SetError(stderr);
            try
            {
                Assert.AreEqual(2, Program.Run(new[] { "--definitely-not-an-option" }));
            }
            finally
            {
                Console.SetError(original);
            }
        }

        [TestMethod]
        public void Run_HelpAndVersion_ReturnZero()
        {
            var stderr = new StringWriter();
            var stdout = new StringWriter();
            var originalErr = Console.Error;
            var originalOut = Console.Out;
            Console.SetError(stderr);
            Console.SetOut(stdout);
            try
            {
                Assert.AreEqual(0, Program.Run(new[] { "--help" }));
                Assert.AreEqual(0, Program.Run(new[] { "--version" }));
            }
            finally
            {
                Console.SetError(originalErr);
                Console.SetOut(originalOut);
            }
        }

        [TestMethod]
        public void Run_PrintOptions_ReturnsZero()
        {
            var stdout = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(stdout);
            try
            {
                Assert.AreEqual(0, Program.Run(new[] { "-c", "x.json", "--print-options", "--dry-run" }));
                StringAssert.Contains(stdout.ToString(), "\"DryRun\": true");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
