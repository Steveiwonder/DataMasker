using System.Data;
using DataMasker.DataSources;
using DataMasker.Models;
using Microsoft.Data.Sqlite;
using Dapper;

namespace DataMasker.Tests.DataSources
{
    [TestClass]
    public class SqlDataSourceTests
    {
        private string _dbName = null!;
        private IDbConnection _keeper = null!;

        [TestInitialize]
        public void Setup()
        {
            _dbName = Guid.NewGuid().ToString("N");
            var cs = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
            _keeper = new SqliteConnection(cs);
            _keeper.Open();
            _keeper.Execute(@"CREATE TABLE [Users] (
                [UserId] INTEGER PRIMARY KEY,
                [FirstName] TEXT,
                [LastName]  TEXT,
                [Password]  TEXT)");
            _keeper.Execute("INSERT INTO [Users] VALUES (1,'Steve','Smith','secret1')");
            _keeper.Execute("INSERT INTO [Users] VALUES (2,'John','Jones','secret2')");
            _keeper.Execute("INSERT INTO [Users] VALUES (3,'Jane','Doe','secret3')");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _keeper?.Dispose();
        }

        private Func<IDbConnection> Factory()
        {
            var cs = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
            return () => new SqliteConnection(cs);
        }

        private static DataSourceConfig MakeConfig(int? batchSize = null, bool dryRun = false) => new DataSourceConfig
        {
            Type = DataSourceType.SqlServer,
            Config = Newtonsoft.Json.Linq.JObject.Parse(@"{ ""connectionString"": ""unused"" }"),
            UpdateBatchSize = batchSize,
            DryRun = dryRun
        };

        private static TableConfig UsersTable() => new TableConfig
        {
            Name = "Users",
            Schema = "main",
            PrimaryKeyColumn = "UserId",
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName },
                new ColumnConfig { Name = "LastName",  Type = DataType.LastName },
                new ColumnConfig { Name = "Password",  Type = DataType.None }
            }
        };

        // ── GetCount ────────────────────────────────────────────────────────

        [TestMethod]
        public void GetCount_ReturnsCorrectCount()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());
            Assert.AreEqual(3, source.GetCount(UsersTable()));
        }

        // ── GetData ─────────────────────────────────────────────────────────

        [TestMethod]
        public void GetData_ReturnsAllRows()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());
            var data = source.GetData(UsersTable()).ToList();
            Assert.AreEqual(3, data.Count);
        }

        [TestMethod]
        public void GetData_RowsContainExpectedColumns()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());
            var row = source.GetData(UsersTable()).First();
            Assert.IsTrue(row.ContainsKey("UserId"));
            Assert.IsTrue(row.ContainsKey("FirstName"));
            Assert.IsTrue(row.ContainsKey("LastName"));
            Assert.IsTrue(row.ContainsKey("Password"));
        }

        [TestMethod]
        public void GetData_ReturnsCorrectValues()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());
            var rows = source.GetData(UsersTable()).ToList();
            var first = rows.First(r => Convert.ToInt32(r["UserId"]) == 1);
            Assert.AreEqual("Steve", first["FirstName"]?.ToString());
            Assert.AreEqual("Smith", first["LastName"]?.ToString());
        }

        // ── UpdateRow ───────────────────────────────────────────────────────

        [TestMethod]
        public void UpdateRow_UpdatesSpecificRow()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());
            var updatedRow = new Dictionary<string, object>
            {
                ["UserId"] = 2,
                ["FirstName"] = "UpdatedJohn",
                ["LastName"] = "UpdatedJones",
                ["Password"] = "newpassword"
            };
            source.UpdateRow(updatedRow, UsersTable());

            // Verify via keeper connection
            var result = _keeper.Query("SELECT * FROM [main].[Users] WHERE [UserId] = 2")
                .Cast<IDictionary<string, object>>().First();
            Assert.AreEqual("UpdatedJohn", result["FirstName"]?.ToString());
            Assert.AreEqual("UpdatedJones", result["LastName"]?.ToString());
        }

        [TestMethod]
        public void UpdateRow_DoesNotAffectOtherRows()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());
            var updatedRow = new Dictionary<string, object>
            {
                ["UserId"] = 2,
                ["FirstName"] = "Changed",
                ["LastName"] = "Changed",
                ["Password"] = "changed"
            };
            source.UpdateRow(updatedRow, UsersTable());

            // Row 1 should be unchanged
            var row1 = _keeper.Query("SELECT * FROM [main].[Users] WHERE [UserId] = 1")
                .Cast<IDictionary<string, object>>().First();
            Assert.AreEqual("Steve", row1["FirstName"]?.ToString());

            // Row 3 should be unchanged
            var row3 = _keeper.Query("SELECT * FROM [main].[Users] WHERE [UserId] = 3")
                .Cast<IDictionary<string, object>>().First();
            Assert.AreEqual("Jane", row3["FirstName"]?.ToString());
        }

        // ── UpdateRows ──────────────────────────────────────────────────────

        [TestMethod]
        public void UpdateRows_WithBatchSize_UpdatesAllRows()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 2), Factory());
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "U1", ["LastName"] = "L1", ["Password"] = "p1" },
                new Dictionary<string, object> { ["UserId"] = 2, ["FirstName"] = "U2", ["LastName"] = "L2", ["Password"] = "p2" },
                new Dictionary<string, object> { ["UserId"] = 3, ["FirstName"] = "U3", ["LastName"] = "L3", ["Password"] = "p3" }
            };

            int lastCallbackValue = 0;
            source.UpdateRows(rows, rows.Count, UsersTable(), count => lastCallbackValue = count);

            Assert.AreEqual(3, lastCallbackValue);

            // Verify all rows updated
            var result = _keeper.Query("SELECT * FROM [main].[Users] ORDER BY [UserId]")
                .Cast<IDictionary<string, object>>().ToList();
            Assert.AreEqual("U1", result[0]["FirstName"]?.ToString());
            Assert.AreEqual("U2", result[1]["FirstName"]?.ToString());
            Assert.AreEqual("U3", result[2]["FirstName"]?.ToString());
        }

        [TestMethod]
        public void UpdateRows_NullBatchSize_UsesRowCount()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: null), Factory());
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "A", ["LastName"] = "B", ["Password"] = "c" }
            };

            source.UpdateRows(rows, rows.Count, UsersTable(), null);

            var result = _keeper.Query("SELECT * FROM [main].[Users] WHERE [UserId] = 1")
                .Cast<IDictionary<string, object>>().First();
            Assert.AreEqual("A", result["FirstName"]?.ToString());
        }

        [TestMethod]
        public void UpdateRows_DryRun_DoesNotPersistChanges()
        {
            var source = new SqlDataSource(MakeConfig(dryRun: true), Factory());
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "DRYRUN", ["LastName"] = "DRYRUN", ["Password"] = "DRYRUN" }
            };

            source.UpdateRows(rows, 1, UsersTable(), null);

            // DryRun rolls back the transaction, so original value should remain
            var result = _keeper.Query("SELECT * FROM [main].[Users] WHERE [UserId] = 1")
                .Cast<IDictionary<string, object>>().First();
            Assert.AreEqual("Steve", result["FirstName"]?.ToString());
        }

        [TestMethod]
        public void UpdateRows_CallbackInvoked_WithCorrectCount()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 1), Factory());
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "A", ["LastName"] = "B", ["Password"] = "c" },
                new Dictionary<string, object> { ["UserId"] = 2, ["FirstName"] = "D", ["LastName"] = "E", ["Password"] = "f" }
            };

            var callbackValues = new List<int>();
            source.UpdateRows(rows, rows.Count, UsersTable(), count => callbackValues.Add(count));

            // batchSize=1: each row is a batch, so 2 callbacks
            Assert.AreEqual(2, callbackValues.Count);
            Assert.AreEqual(1, callbackValues[0]);
            Assert.AreEqual(2, callbackValues[1]);
        }
    }
}
