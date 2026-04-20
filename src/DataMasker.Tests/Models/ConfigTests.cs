using DataMasker.Models;

namespace DataMasker.Tests.Models
{
    [TestClass]
    public class ConfigTests
    {
        [TestMethod]
        public void Load_ValidConfigFile_ReturnsConfigObject()
        {
            var config = Config.Load("config1.json");

            Assert.IsNotNull(config);
            Assert.IsNotNull(config.DataSource);
            Assert.IsNotNull(config.DataGeneration);
            Assert.IsNotNull(config.Tables);
            Assert.IsTrue(config.Tables.Count > 0);
        }

        [TestMethod]
        public void Load_MissingFile_ThrowsFileNotFoundException()
        {
            try
            {
                Config.Load("does_not_exist.json");
                Assert.Fail("Expected FileNotFoundException to be thrown");
            }
            catch (FileNotFoundException) { }
        }

        [TestMethod]
        public void Load_ConfigWithNoTables_ThrowsInvalidOperationException()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, @"{
                    ""dataSource"": {
                        ""type"": ""SqlServer"",
                        ""config"": { ""connectionString"": ""Server=.;Database=test;"" }
                    }
                }");
                try
                {
                    Config.Load(tempFile);
                    Assert.Fail("Expected InvalidOperationException to be thrown");
                }
                catch (InvalidOperationException) { }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void Load_DataGenerationDefaultsWhenMissing()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, @"{
                    ""dataSource"": {
                        ""type"": ""SqlServer"",
                        ""config"": { ""connectionString"": ""Server=.;Database=test;"" }
                    },
                    ""tables"": [
                        {
                            ""name"": ""Users"",
                            ""primaryKeyColumn"": ""Id"",
                            ""columns"": []
                        }
                    ]
                }");
                var config = Config.Load(tempFile);
                Assert.IsNotNull(config.DataGeneration, "DataGeneration should be defaulted when not provided");
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
