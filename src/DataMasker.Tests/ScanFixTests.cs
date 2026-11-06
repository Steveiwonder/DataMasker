using DataMasker.DataSources;
using DataMasker.Models;
using DataMasker.Utils;
using DMUtils = DataMasker.Utils.Utils;

namespace DataMasker.Tests
{
    /// <summary>
    /// Regression tests for the smaller fixes: in-memory data source, None/Bogus column handling,
    /// identifier escaping, connection string errors and tablesConfigPath resolution.
    /// </summary>
    [TestClass]
    public class ScanFixTests
    {
        private static TableConfig UsersTable() => new TableConfig
        {
            Name = "Users",
            PrimaryKeyColumn = "UserId",
            Columns = new List<ColumnConfig>()
        };

        [TestMethod]
        public void InMemory_UpdateRow_IsVisibleThroughGetData()
        {
            var source = new InMemoryFakeDataSource();
            var table = UsersTable();

            source.UpdateRow(new Dictionary<string, object> { ["UserId"] = 2, ["FirstName"] = "Changed" }, table);

            var row = source.GetData(table).Single(r => (int)r["UserId"] == 2);
            Assert.AreEqual("Changed", row["FirstName"]);
        }

        [TestMethod]
        public void InMemory_EveryRow_HasTheSameColumnNames()
        {
            var rows = new InMemoryFakeDataSource().GetData(UsersTable()).ToList();
            foreach (var row in rows)
            {
                Assert.IsTrue(row.ContainsKey("FirstName"), "FirstName key should not carry stray whitespace");
            }
        }

        [TestMethod]
        public void InMemory_UpdateRow_UsesConfiguredPrimaryKey()
        {
            var source = new InMemoryFakeDataSource();
            var table = new TableConfig { Name = "Users", PrimaryKeyColumn = "ContactNumber", Columns = new List<ColumnConfig>() };

            source.UpdateRow(new Dictionary<string, object> { ["ContactNumber"] = "+1 555-555-555-555-555-55-57", ["FirstName"] = "ByPhone" }, table);

            Assert.AreEqual("ByPhone", source.GetData(table).Single(r => (string)r["ContactNumber"] == "+1 555-555-555-555-555-55-57")["FirstName"]);
        }

        [TestMethod]
        public void InMemory_UpdateRow_UnknownKey_Throws()
        {
            var source = new InMemoryFakeDataSource();
            Assert.ThrowsExactly<InvalidOperationException>(() => source.UpdateRow(new Dictionary<string, object> { ["UserId"] = 99 }, UsersTable()));
        }

        [TestMethod]
        public void InMemory_UpdateRows_InvokesCallback()
        {
            var source = new InMemoryFakeDataSource();
            var callbacks = new List<int>();
            var rows = source.GetData(UsersTable()).ToList();

            source.UpdateRows(rows, rows.Count, UsersTable(), callbacks.Add);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, callbacks);
        }

        [TestMethod]
        public void InMemory_GetDistinctValues_ReturnsDistinctNonNull()
        {
            var values = new InMemoryFakeDataSource().GetDistinctValues(UsersTable(), "LastName").ToList();
            CollectionAssert.AreEquivalent(new object[] { "Smith", "Lucas" }, values);
        }

        [TestMethod]
        public void Bogus_NoneType_WithoutValueSource_ThrowsDescriptiveError()
        {
            var provider = new BogusDataProvider(new DataGenerationConfig());
            var column = new ColumnConfig { Name = "Status", Type = DataType.None };

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetValue(column, new Dictionary<string, object> { ["Status"] = "active" }, null));
            StringAssert.Contains(ex.Message, "Status");
            StringAssert.Contains(ex.Message, "useValue");
        }

        [TestMethod]
        public void Bogus_BogusType_WithUseValue_ReturnsFixedValue()
        {
            var provider = new BogusDataProvider(new DataGenerationConfig());
            var column = new ColumnConfig { Name = "City", Type = DataType.Bogus, UseValue = "Fixed" };

            Assert.AreEqual("Fixed", provider.GetValue(column, new Dictionary<string, object> { ["City"] = "x" }, null));
        }

        [TestMethod]
        public void SqlProvider_MissingSqlValue_ThrowsDescriptiveError()
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            var provider = new SqlDataProvider(conn);
            var column = new ColumnConfig { Name = "Password", Type = DataType.Sql };

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetValue(column, new Dictionary<string, object> { ["Password"] = "x" }, null));
            StringAssert.Contains(ex.Message, "sqlValue");
        }

        [TestMethod]
        public void SqlProvider_OpensConnectionOnceAndLeavesItOpen()
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            var provider = new SqlDataProvider(conn);
            var column = new ColumnConfig { Name = "Password", Type = DataType.Sql, SqlValue = new SqlValueConfig { Query = "SELECT 'x'" } };

            provider.GetValue(column, new Dictionary<string, object> { ["Password"] = "a" }, null);
            Assert.AreEqual(System.Data.ConnectionState.Open, conn.State);
            provider.GetValue(column, new Dictionary<string, object> { ["Password"] = "b" }, null);
            Assert.AreEqual(System.Data.ConnectionState.Open, conn.State);
        }

        [TestMethod]
        public void MakeColumnNameSafe_EscapesClosingBracket()
        {
            Assert.AreEqual("[a]]b]", DMUtils.MakeColumnNameSafe("a]b"));
        }

        [TestMethod]
        public void MakeColumnNameSafe_Empty_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => DMUtils.MakeColumnNameSafe(" "));
        }

        [TestMethod]
        public void GetConnectionString_MissingConfigSection_ThrowsDescriptiveError()
        {
            var config = new DataSourceConfig { Type = DataSourceType.SqlServer, Config = null };
            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => config.GetConnectionString());
            StringAssert.Contains(ex.Message, "dataSource.config");
        }

        [TestMethod]
        public void Config_TablesConfigPath_ResolvesRelativeToConfigFileDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "datamasker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "tables.json"), @"[ { ""name"": ""Orders"", ""primaryKeyColumn"": ""OrderId"", ""columns"": [] } ]");
                string configFile = Path.Combine(directory, "config.json");
                File.WriteAllText(configFile, @"{
                    ""dataSource"": { ""type"": ""SqlServer"", ""config"": { ""connectionString"": ""Server=.;"" } },
                    ""tablesConfigPath"": ""tables.json""
                }");

                // The current directory is the test output folder, which has no tables.json.
                var config = Config.Load(configFile);

                Assert.AreEqual(1, config.Tables.Count);
                Assert.AreEqual("Orders", config.Tables[0].Name);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void Config_TablesConfigPath_StillMissing_ThrowsFileNotFound()
        {
            string configFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(configFile, @"{
                    ""dataSource"": { ""type"": ""SqlServer"", ""config"": { ""connectionString"": ""Server=.;"" } },
                    ""tablesConfigPath"": ""nope-" + Guid.NewGuid().ToString("N") + @".json""
                }");
                Assert.ThrowsExactly<FileNotFoundException>(() => Config.Load(configFile));
            }
            finally
            {
                File.Delete(configFile);
            }
        }

        [TestMethod]
        public void SqlStatementDataSource_MissingStatement_ThrowsDescriptiveError()
        {
            var config = new DataSourceConfig
            {
                Type = DataSourceType.SqlServer,
                Config = Newtonsoft.Json.Linq.JObject.Parse(@"{ ""connectionString"": ""Server=.;"" }")
            };
            var source = new SqlStatementDataSource(config);

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => source.Execute(new SqlStatementConfig { Name = "empty", Statement = " " }));
            StringAssert.Contains(ex.Message, "empty");
        }
    }
}
