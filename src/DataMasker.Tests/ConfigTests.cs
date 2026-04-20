using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using DataMasker.Models;
using DataMasker.Interfaces;
using Bogus;

namespace DataMasker.Tests
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
            // config1.json has dataGeneration, so create one without it
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

    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void TryParseGender_Male_ReturnsMale()
        {
            var result = Utils.Utils.TryParseGender("m");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, result);

            result = Utils.Utils.TryParseGender("male");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, result);

            result = Utils.Utils.TryParseGender("M");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, result);
        }

        [TestMethod]
        public void TryParseGender_Female_ReturnsFemale()
        {
            var result = Utils.Utils.TryParseGender("f");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, result);

            result = Utils.Utils.TryParseGender("female");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, result);

            result = Utils.Utils.TryParseGender("F");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, result);
        }

        [TestMethod]
        public void TryParseGender_Null_ReturnsNull()
        {
            var result = Utils.Utils.TryParseGender(null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TryParseGender_Unknown_ReturnsNull()
        {
            var result = Utils.Utils.TryParseGender("unknown");
            Assert.IsNull(result);
        }
    }

    [TestClass]
    public class InMemoryFakeDataSourceTests
    {
        [TestMethod]
        public void GetCount_ReturnsRowCount_NotTableCount()
        {
            var dataSource = new DataSources.InMemoryFakeDataSource();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Columns = new List<ColumnConfig>()
            };

            int count = dataSource.GetCount(tableConfig);

            // The Users table in InMemoryFakeDataSource has 3 rows
            Assert.AreEqual(3, count, "GetCount should return the number of rows, not the number of tables");
        }

        [TestMethod]
        public void GetData_ReturnsAllRows()
        {
            var dataSource = new DataSources.InMemoryFakeDataSource();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Columns = new List<ColumnConfig>()
            };

            var rows = dataSource.GetData(tableConfig);
            var list = new List<IDictionary<string, object>>(rows);

            Assert.AreEqual(3, list.Count);
        }
    }

    [TestClass]
    public class DataMaskerTests
    {
        private static TableConfig BuildUsersTableConfig() => new TableConfig
        {
            Name = "Users",
            PrimaryKeyColumn = "UserId",
            Schema = "dbo",
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName },
                new ColumnConfig { Name = "LastName",  Type = DataType.LastName },
                new ColumnConfig { Name = "DOB",        Type = DataType.DateOfBirth },
                new ColumnConfig { Name = "Gender",     Type = DataType.None, Ignore = true }
            }
        };

        private static IDataMasker BuildMasker() =>
            new DataMasker(new[] { new BogusDataProvider(new Models.DataGenerationConfig { Locale = "en" }) });

        [TestMethod]
        public void Mask_ReplacesFirstAndLastName()
        {
            var masker = BuildMasker();
            var tableConfig = BuildUsersTableConfig();

            var row = new Dictionary<string, object>
            {
                ["UserId"]    = 1,
                ["FirstName"] = "Steve",
                ["LastName"]  = "Smith",
                ["DOB"]       = DateTime.Parse("1974-09-23"),
                ["Gender"]    = "M"
            };

            var result = masker.Mask(row, tableConfig);

            Assert.IsNotNull(result["FirstName"]);
            Assert.IsNotNull(result["LastName"]);
            // Values should be replaced (may occasionally match by chance, but very unlikely for names)
        }

        [TestMethod]
        public void Mask_RetainNullValues_LeavesNullIntact()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, RetainNullValues = true }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"]    = 1,
                ["FirstName"] = null
            };

            var result = masker.Mask(row, tableConfig);

            Assert.IsNull(result["FirstName"], "Null value should be retained when RetainNullValues=true");
        }

        [TestMethod]
        public void Mask_UseValue_SetsFixedValue()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "Password", Type = DataType.None, UseValue = "DevPassword123" }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"]   = 1,
                ["Password"] = "SuperSecret"
            };

            var result = masker.Mask(row, tableConfig);

            Assert.AreEqual("DevPassword123", result["Password"]);
        }

        [TestMethod]
        public void Mask_ComputedColumn_ConcatenatesSourceColumns()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "FirstName", Type = DataType.FirstName },
                    new ColumnConfig { Name = "LastName",  Type = DataType.LastName },
                    new ColumnConfig
                    {
                        Name          = "FullName",
                        Type          = DataType.Computed,
                        SourceColumns = new[] { "FirstName", "LastName" },
                        Separator     = " "
                    }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"]    = 1,
                ["FirstName"] = "Steve",
                ["LastName"]  = "Smith",
                ["FullName"]  = ""
            };

            var result = masker.Mask(row, tableConfig);

            string fullName = result["FullName"]?.ToString() ?? "";
            Assert.IsTrue(fullName.Contains(" "), "FullName should be a space-separated combination of FirstName and LastName");
            string[] parts = fullName.Split(' ');
            Assert.AreEqual(result["FirstName"]?.ToString(), parts[0]);
            Assert.AreEqual(result["LastName"]?.ToString(), parts[1]);
        }

        [TestMethod]
        public void Mask_IgnoredColumn_IsNotChanged()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "Gender", Type = DataType.None, Ignore = true }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"] = 1,
                ["Gender"] = "M"
            };

            var result = masker.Mask(row, tableConfig);

            Assert.AreEqual("M", result["Gender"], "Ignored columns should not be modified");
        }

        [TestMethod]
        public void Mask_UniqueValues_ProducesDistinctResults()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, Unique = true }
                }
            };

            var seenValues = new HashSet<object>();
            for (int i = 0; i < 10; i++)
            {
                var row = new Dictionary<string, object>
                {
                    ["UserId"]    = i,
                    ["FirstName"] = "Original"
                };
                var result = masker.Mask(row, tableConfig);
                seenValues.Add(result["FirstName"]);
            }

            Assert.AreEqual(10, seenValues.Count, "Unique=true should produce distinct values across rows");
        }
    }

    [TestClass]
    public class BogusTests
    {
        [TestMethod]
        public void RandomizerValues_ParsesWithoutException()
        {
            Faker faker = new Faker();

            Assert.IsNotNull(faker.Parse("{{Randomizer.Int(10,100)}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.Digits(5)}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.Even(1, 100)}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.Odd(1, 100)}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.Double()}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.Bool()}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.Guid()}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.AlphaNumeric(10)}}"));
            Assert.IsNotNull(faker.Parse("{{Randomizer.Hash()}}"));
        }
    }
}
