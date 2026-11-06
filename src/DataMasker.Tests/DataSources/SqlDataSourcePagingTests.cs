using System.Data;
using Dapper;
using DataMasker.DataSources;
using DataMasker.Models;
using Microsoft.Data.Sqlite;

namespace DataMasker.Tests.DataSources
{
    /// <summary>
    /// Covers the paging, distinct value and identifier handling of <see cref="SqlDataSource"/>
    /// using SQLite (the non SQL Server code path).
    /// </summary>
    [TestClass]
    public class SqlDataSourcePagingTests
    {
        private string _dbName = null!;
        private IDbConnection _keeper = null!;

        [TestInitialize]
        public void Setup()
        {
            _dbName = Guid.NewGuid().ToString("N");
            _keeper = new SqliteConnection(ConnectionString);
            _keeper.Open();
            _keeper.Execute(@"CREATE TABLE [Users] (
                [UserId] INTEGER PRIMARY KEY,
                [FirstName] TEXT,
                [Odd Name] TEXT)");
            for (int i = 1; i <= 7; i++)
            {
                _keeper.Execute("INSERT INTO [Users] VALUES (@id, @name, @odd)", new { id = i, name = $"Name{i}", odd = i % 2 == 0 ? null : "odd" });
            }
        }

        [TestCleanup]
        public void Cleanup() => _keeper?.Dispose();

        private string ConnectionString => $"Data Source={_dbName};Mode=Memory;Cache=Shared";

        private Func<IDbConnection> Factory() => () => new SqliteConnection(ConnectionString);

        private static DataSourceConfig MakeConfig(int? batchSize = null) => new DataSourceConfig
        {
            Type = DataSourceType.SqlServer,
            Config = Newtonsoft.Json.Linq.JObject.Parse(@"{ ""connectionString"": ""unused"" }"),
            UpdateBatchSize = batchSize
        };

        private static TableConfig UsersTable() => new TableConfig
        {
            Name = "Users",
            Schema = "main",
            PrimaryKeyColumn = "UserId",
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName },
                new ColumnConfig { Name = "Odd Name", Type = DataType.None, Ignore = true }
            }
        };

        [TestMethod]
        public void GetData_PageSmallerThanTable_ReturnsEveryRowOnceInKeyOrder()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 3), Factory());

            var ids = source.GetData(UsersTable()).Select(r => Convert.ToInt32(r["UserId"])).ToList();

            CollectionAssert.AreEqual(Enumerable.Range(1, 7).ToList(), ids);
        }

        [TestMethod]
        public void GetData_PageSizeDividesRowCountExactly_DoesNotLoop()
        {
            _keeper.Execute("DELETE FROM [Users] WHERE [UserId] = 7");
            var source = new SqlDataSource(MakeConfig(batchSize: 3), Factory());

            Assert.AreEqual(6, source.GetData(UsersTable()).Count());
        }

        [TestMethod]
        public void GetData_EmptyTable_ReturnsNothing()
        {
            _keeper.Execute("DELETE FROM [Users]");
            var source = new SqlDataSource(MakeConfig(batchSize: 3), Factory());

            Assert.AreEqual(0, source.GetData(UsersTable()).Count());
        }

        [TestMethod]
        public void GetData_IsLazy_RowsAreFetchedAsTheyAreConsumed()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 2), Factory());

            var first = source.GetData(UsersTable()).First();
            // Change a later row after the first page was read: it should be visible because
            // later pages are read on demand rather than up front.
            _keeper.Execute("UPDATE [Users] SET [FirstName] = 'Late' WHERE [UserId] = 7");
            var last = source.GetData(UsersTable()).Last();

            Assert.AreEqual("Name1", first["FirstName"]);
            Assert.AreEqual("Late", last["FirstName"]);
        }

        [TestMethod]
        public void GetData_ColumnNameContainingSpace_IsQuoted()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());

            var row = source.GetData(UsersTable()).First();

            Assert.IsTrue(row.ContainsKey("Odd Name"));
        }

        [TestMethod]
        public void GetDistinctValues_ExcludesNullsAndDuplicates()
        {
            var source = new SqlDataSource(MakeConfig(), Factory());

            var values = source.GetDistinctValues(UsersTable(), "Odd Name").ToList();

            CollectionAssert.AreEquivalent(new object[] { "odd" }, values);
        }

        [TestMethod]
        public void UpdateRows_NoBatchSize_UsesDefaultOfOneThousand()
        {
            Assert.AreEqual(1000, SqlDataSource.DefaultBatchSize);

            var source = new SqlDataSource(MakeConfig(batchSize: null), Factory());
            var callbacks = new List<int>();
            var updates = Enumerable.Range(1, 7).Select(id => (IDictionary<string, object>)new Dictionary<string, object> { ["UserId"] = id, ["FirstName"] = $"U{id}" });

            source.UpdateRows(updates, 7, UsersTable(), callbacks.Add);

            CollectionAssert.AreEqual(new[] { 7 }, callbacks, "seven rows fit in one default sized batch");
            Assert.AreEqual("U7", _keeper.ExecuteScalar<string>("SELECT [FirstName] FROM [Users] WHERE [UserId] = 7"));
        }

        [TestMethod]
        public void UpdateRows_EmptyInput_DoesNothing()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 2), Factory());
            var callbacks = new List<int>();

            source.UpdateRows(Enumerable.Empty<IDictionary<string, object>>(), 0, UsersTable(), callbacks.Add);

            Assert.AreEqual(0, callbacks.Count);
        }

        [TestMethod]
        public void EndToEnd_ReadMaskUpdate_WithSmallBatches()
        {
            var source = new SqlDataSource(MakeConfig(batchSize: 2), Factory());
            var table = UsersTable();
            var masker = new DataMasker(new Interfaces.IDataProvider[] { new BogusDataProvider(new DataGenerationConfig { Locale = "en" }) });

            var masked = source.GetData(table).Select(row => masker.Mask(row, table));
            source.UpdateRows(masked, 7, table, null!);

            var names = _keeper.Query<string>("SELECT [FirstName] FROM [Users] ORDER BY [UserId]").ToList();
            Assert.AreEqual(7, names.Count);
            Assert.IsTrue(names.All(n => !n.StartsWith("Name")), "every row should have been masked");
        }
    }
}
