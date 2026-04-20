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

        [TestMethod]
        public void Load_TablesConfigPath_LoadsTablesFromExternalFile()
        {
            string tableFile  = Path.GetTempFileName();
            string configFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tableFile, @"[
                    {
                        ""name"": ""Orders"",
                        ""primaryKeyColumn"": ""OrderId"",
                        ""columns"": []
                    }
                ]");

                File.WriteAllText(configFile, $@"{{
                    ""dataSource"": {{
                        ""type"": ""SqlServer"",
                        ""config"": {{ ""connectionString"": ""Server=.;Database=test;"" }}
                    }},
                    ""tablesConfigPath"": ""{tableFile.Replace("\\", "\\\\")}""
                }}");

                var config = Config.Load(configFile);

                Assert.IsNotNull(config.Tables, "Tables should be loaded from external file");
                Assert.AreEqual(1, config.Tables.Count);
                Assert.AreEqual("Orders", config.Tables[0].Name);
            }
            finally
            {
                File.Delete(tableFile);
                File.Delete(configFile);
            }
        }

        [TestMethod]
        public void Load_TablesConfigPath_FileNotFound_ThrowsFileNotFoundException()
        {
            string configFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(configFile, @"{
                    ""dataSource"": {
                        ""type"": ""SqlServer"",
                        ""config"": { ""connectionString"": ""Server=.;Database=test;"" }
                    },
                    ""tablesConfigPath"": ""this_file_does_not_exist_tables.json""
                }");
                try
                {
                    Config.Load(configFile);
                    Assert.Fail("Expected FileNotFoundException for missing tablesConfigPath file");
                }
                catch (FileNotFoundException) { }
            }
            finally
            {
                File.Delete(configFile);
            }
        }

        [TestMethod]
        public void Load_BothTablesAndTablesConfigPath_TablesConfigPathWins()
        {
            string tableFile  = Path.GetTempFileName();
            string configFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tableFile, @"[
                    {
                        ""name"": ""ExternalTable"",
                        ""primaryKeyColumn"": ""Id"",
                        ""columns"": []
                    }
                ]");

                File.WriteAllText(configFile, $@"{{
                    ""dataSource"": {{
                        ""type"": ""SqlServer"",
                        ""config"": {{ ""connectionString"": ""Server=.;Database=test;"" }}
                    }},
                    ""tables"": [
                        {{
                            ""name"": ""InlineTable"",
                            ""primaryKeyColumn"": ""Id"",
                            ""columns"": []
                        }}
                    ],
                    ""tablesConfigPath"": ""{tableFile.Replace("\\", "\\\\")}""
                }}");

                var config = Config.Load(configFile);

                Assert.AreEqual("ExternalTable", config.Tables[0].Name,
                    "tablesConfigPath should override inline tables when both are supplied");
            }
            finally
            {
                File.Delete(tableFile);
                File.Delete(configFile);
            }
        }
    }
}
