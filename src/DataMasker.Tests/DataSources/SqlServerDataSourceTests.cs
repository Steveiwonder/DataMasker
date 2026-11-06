using Dapper;
using DataMasker.DataSources;
using DataMasker.Interfaces;
using DataMasker.Models;
using Microsoft.Data.SqlClient;

namespace DataMasker.Tests.DataSources
{
    /// <summary>
    /// Integration tests for the SQL Server specific paths (keyset paging with TOP, SqlBulkCopy
    /// based updates). They run against SQL Server LocalDB and are marked inconclusive when it
    /// is not installed, which is the case on the Linux CI runner.
    /// </summary>
    [TestClass]
    public class SqlServerDataSourceTests
    {
        private const string ConnectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=tempdb;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5";

        private const int RowCount = 25;

        private SqlConnection _connection = null!;
        private string _table = null!;

        [TestInitialize]
        public void Setup()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("SQL Server LocalDB is only available on Windows.");
            }

            _connection = new SqlConnection(ConnectionString);
            try
            {
                _connection.Open();
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"SQL Server LocalDB is not available: {ex.Message}");
            }

            _table = "DataMaskerTest_" + Guid.NewGuid().ToString("N");
            _connection.Execute($@"CREATE TABLE [dbo].[{_table}] (
                [UserId]    INT IDENTITY(1,1) PRIMARY KEY,
                [FirstName] NVARCHAR(50) NOT NULL,
                [LastName]  NVARCHAR(50) NULL,
                [Email]     NVARCHAR(100) NOT NULL,
                [DOB]       DATE NULL,
                CONSTRAINT [UQ_{_table}_Email] UNIQUE ([Email]))");

            for (int i = 0; i < RowCount; i++)
            {
                _connection.Execute(
                    $"INSERT INTO [dbo].[{_table}] ([FirstName], [LastName], [Email], [DOB]) VALUES (@first, @last, @email, @dob)",
                    new { first = $"First{i}", last = $"Last{i}", email = $"user{i:00}@example.com", dob = new DateTime(1980, 1, 1).AddDays(i) });
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_connection?.State == System.Data.ConnectionState.Open)
            {
                _connection.Execute($"IF OBJECT_ID('[dbo].[{_table}]') IS NOT NULL DROP TABLE [dbo].[{_table}]");
            }
            _connection?.Dispose();
        }

        private DataSourceConfig MakeConfig(int? batchSize = null, bool dryRun = false) => new DataSourceConfig
        {
            Type = DataSourceType.SqlServer,
            Config = Newtonsoft.Json.Linq.JObject.Parse($@"{{ ""connectionString"": ""{ConnectionString.Replace("\\", "\\\\")}"" }}"),
            UpdateBatchSize = batchSize,
            DryRun = dryRun
        };

        private TableConfig UsersTable() => new TableConfig
        {
            Name = _table,
            Schema = "dbo",
            PrimaryKeyColumn = "UserId",
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName },
                new ColumnConfig { Name = "LastName",  Type = DataType.LastName },
                new ColumnConfig { Name = "Email",     Type = DataType.StringFormat, StringFormatPattern = "user##@example.com", Unique = true },
                new ColumnConfig { Name = "DOB",       Type = DataType.DateOfBirth }
            }
        };

        private List<dynamic> ReadAll() =>
            _connection.Query($"SELECT * FROM [dbo].[{_table}] ORDER BY [UserId]").ToList();

        [TestMethod]
        public void GetData_PagesThroughAllRowsInPrimaryKeyOrder()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 10));

            var rows = source.GetData(UsersTable()).ToList();

            Assert.AreEqual(RowCount, rows.Count);
            for (int i = 0; i < RowCount; i++)
            {
                Assert.AreEqual(i + 1, Convert.ToInt32(rows[i]["UserId"]), "rows should be returned in primary key order");
                Assert.AreEqual($"First{i}", rows[i]["FirstName"]);
            }
        }

        [TestMethod]
        public void GetData_BatchSizeLargerThanTable_ReturnsAllRows()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 1000));
            Assert.AreEqual(RowCount, source.GetData(UsersTable()).Count());
        }

        [TestMethod]
        public void UpdateRows_BulkUpdatesEveryBatch()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 10));
            var callbacks = new List<int>();

            var updates = Enumerable.Range(1, RowCount).Select(id => (IDictionary<string, object>)new Dictionary<string, object>
            {
                ["UserId"] = id,
                ["FirstName"] = $"New{id}",
                ["LastName"] = $"Name{id}",
                ["Email"] = $"new{id:00}@example.com",
                ["DOB"] = new DateTime(2000, 1, 1).AddDays(id)
            });

            source.UpdateRows(updates, RowCount, UsersTable(), callbacks.Add);

            CollectionAssert.AreEqual(new[] { 10, 20, 25 }, callbacks);

            var rows = ReadAll();
            Assert.AreEqual(RowCount, rows.Count);
            for (int i = 0; i < RowCount; i++)
            {
                int id = i + 1;
                Assert.AreEqual($"New{id}", (string)rows[i].FirstName);
                Assert.AreEqual($"Name{id}", (string)rows[i].LastName);
                Assert.AreEqual($"new{id:00}@example.com", (string)rows[i].Email);
                Assert.AreEqual(new DateTime(2000, 1, 1).AddDays(id), (DateTime)rows[i].DOB);
            }
        }

        [TestMethod]
        public void UpdateRows_NullValues_AreWrittenAsNull()
        {
            var source = new SqlDataSource(MakeConfig());
            var updates = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "X", ["LastName"] = null!, ["Email"] = "x@example.com", ["DOB"] = null! }
            };

            source.UpdateRows(updates, 1, UsersTable(), null!);

            var row = _connection.QuerySingle($"SELECT * FROM [dbo].[{_table}] WHERE [UserId] = 1");
            Assert.IsNull(row.LastName);
            Assert.IsNull(row.DOB);
        }

        [TestMethod]
        public void UpdateRows_DryRun_RollsBack()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 5, dryRun: true));
            var updates = Enumerable.Range(1, RowCount).Select(id => (IDictionary<string, object>)new Dictionary<string, object>
            {
                ["UserId"] = id,
                ["FirstName"] = "Changed",
                ["LastName"] = "Changed",
                ["Email"] = $"changed{id:00}@example.com",
                ["DOB"] = null!
            });

            source.UpdateRows(updates, RowCount, UsersTable(), null!);

            var rows = ReadAll();
            Assert.IsTrue(rows.All(r => ((string)r.FirstName).StartsWith("First")), "dry run must not persist changes");
        }

        [TestMethod]
        public void GetDistinctValues_ReturnsExistingNonNullValues()
        {
            _connection.Execute($"UPDATE [dbo].[{_table}] SET [LastName] = NULL WHERE [UserId] <= 5");
            _connection.Execute($"UPDATE [dbo].[{_table}] SET [LastName] = 'Shared' WHERE [UserId] > 5");
            var source = new SqlDataSource(MakeConfig());

            var values = source.GetDistinctValues(UsersTable(), "LastName").ToList();

            CollectionAssert.AreEquivalent(new object[] { "Shared" }, values);
        }

        [TestMethod]
        public void GetCount_ReturnsRowCount()
        {
            Assert.AreEqual(RowCount, new SqlDataSource(MakeConfig()).GetCount(UsersTable()));
        }

        [TestMethod]
        public void EndToEnd_UniqueColumnWithUniqueIndex_DoesNotCollideWithExistingValues()
        {
            // 25 of the 100 possible generated emails already exist. Batches of 5 mean a generated
            // value equal to a not-yet-updated row would violate the unique index unless the
            // existing values were seeded first.
            var source = new SqlDataSource(MakeConfig(batchSize: 5));
            var table = UsersTable();
            var masker = new DataMasker(new IDataProvider[] { new BogusDataProvider(new DataGenerationConfig { Locale = "en" }) });

            masker.SeedUniqueValues(source, table);
            var masked = source.GetData(table).Select(row => masker.Mask(row, table));
            source.UpdateRows(masked, RowCount, table, null!);

            var rows = ReadAll();
            var emails = rows.Select(r => (string)r.Email).ToList();
            Assert.AreEqual(RowCount, emails.Distinct().Count());
            Assert.IsTrue(emails.All(e => !e.StartsWith("user") || int.Parse(e.Substring(4, 2)) >= RowCount), "no masked email should equal one of the original values");
            Assert.IsTrue(rows.All(r => !((string)r.FirstName).StartsWith("First")), "all first names should be masked");
        }

        [TestMethod]
        public void Runner_EndToEnd_ReturnsZeroAndMasksData()
        {
            string configFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(configFile, $@"{{
                    ""dataSource"": {{
                        ""type"": ""SqlServer"",
                        ""updateBatchSize"": 7,
                        ""config"": {{ ""connectionString"": ""{ConnectionString.Replace("\\", "\\\\")}"" }}
                    }},
                    ""tables"": [
                        {{
                            ""name"": ""{_table}"",
                            ""primaryKeyColumn"": ""UserId"",
                            ""columns"": [
                                {{ ""name"": ""FirstName"", ""type"": ""FirstName"" }},
                                {{ ""name"": ""Email"", ""type"": ""StringFormat"", ""stringFormatPattern"": ""user##@example.com"", ""unique"": true }}
                            ]
                        }}
                    ]
                }}");

                int exitCode = global::DataMasker.Runner.Program.Run(new[] { "-c", configFile, "--no-output" });

                Assert.AreEqual(0, exitCode);
                var rows = ReadAll();
                Assert.IsTrue(rows.All(r => !((string)r.FirstName).StartsWith("First")), "runner should have masked every row");
                Assert.AreEqual(RowCount, rows.Select(r => (string)r.Email).Distinct().Count());
            }
            finally
            {
                File.Delete(configFile);
            }
        }
    }
}
