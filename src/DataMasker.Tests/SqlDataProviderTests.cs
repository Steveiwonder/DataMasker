using System.Data;
using DataMasker.Models;
using Microsoft.Data.Sqlite;
using Dapper;

namespace DataMasker.Tests
{
    [TestClass]
    public class SqlDataProviderTests
    {
        private static IDbConnection CreateLookupDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            conn.Execute(@"CREATE TABLE [Lookup] (
                [Key] TEXT PRIMARY KEY,
                [MaskedValue] TEXT)");
            conn.Execute("INSERT INTO [Lookup] VALUES ('Steve','MaskedSteve')");
            conn.Execute("INSERT INTO [Lookup] VALUES ('John','MaskedJohn')");
            return conn;
        }

        [TestMethod]
        public void CanProvide_Sql_ReturnsTrue()
        {
            using var conn = CreateLookupDb();
            var provider = new SqlDataProvider(conn);
            Assert.IsTrue(provider.CanProvide(DataType.Sql));
        }

        [TestMethod]
        public void CanProvide_NonSqlTypes_ReturnsFalse()
        {
            using var conn = CreateLookupDb();
            var provider = new SqlDataProvider(conn);
            Assert.IsFalse(provider.CanProvide(DataType.FirstName));
            Assert.IsFalse(provider.CanProvide(DataType.LastName));
            Assert.IsFalse(provider.CanProvide(DataType.Bogus));
            Assert.IsFalse(provider.CanProvide(DataType.Computed));
            Assert.IsFalse(provider.CanProvide(DataType.None));
        }

        [TestMethod]
        public void GetValue_ScalarQuery_ReturnsQueryResult()
        {
            using var conn = CreateLookupDb();
            var provider = new SqlDataProvider(conn);
            var col = new ColumnConfig
            {
                Name = "FirstName",
                Type = DataType.Sql,
                SqlValue = new SqlValueConfig
                {
                    Query = "SELECT 'MASKED'",
                    ValueHandling = NotFoundValueHandling.Null
                }
            };
            var row = new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "Steve" };
            var result = provider.GetValue(col, row, null);
            Assert.AreEqual("MASKED", result);
        }

        [TestMethod]
        public void GetValue_QueryReturnsNull_KeepValue_ReturnsOriginalValue()
        {
            using var conn = CreateLookupDb();
            var provider = new SqlDataProvider(conn);
            var col = new ColumnConfig
            {
                Name = "FirstName",
                Type = DataType.Sql,
                SqlValue = new SqlValueConfig
                {
                    Query = "SELECT NULL",
                    ValueHandling = NotFoundValueHandling.KeepValue
                }
            };
            var row = new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "OriginalValue" };
            var result = provider.GetValue(col, row, null);
            Assert.AreEqual("OriginalValue", result);
        }

        [TestMethod]
        public void GetValue_QueryReturnsNull_NullHandling_ReturnsNull()
        {
            using var conn = CreateLookupDb();
            var provider = new SqlDataProvider(conn);
            var col = new ColumnConfig
            {
                Name = "FirstName",
                Type = DataType.Sql,
                SqlValue = new SqlValueConfig
                {
                    Query = "SELECT NULL",
                    ValueHandling = NotFoundValueHandling.Null
                }
            };
            var row = new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "OriginalValue" };
            var result = provider.GetValue(col, row, null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetValue_QueryUsesRowParameters()
        {
            using var conn = CreateLookupDb();
            var provider = new SqlDataProvider(conn);
            var col = new ColumnConfig
            {
                Name = "FirstName",
                Type = DataType.Sql,
                SqlValue = new SqlValueConfig
                {
                    Query = "SELECT [MaskedValue] FROM [Lookup] WHERE [Key] = @FirstName",
                    ValueHandling = NotFoundValueHandling.KeepValue
                }
            };
            var row = new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "Steve" };
            var result = provider.GetValue(col, row, null);
            Assert.AreEqual("MaskedSteve", result);
        }

        [TestMethod]
        public void GetValue_QueryNoMatch_KeepValue_ReturnsOriginal()
        {
            using var conn = CreateLookupDb();
            var provider = new SqlDataProvider(conn);
            var col = new ColumnConfig
            {
                Name = "FirstName",
                Type = DataType.Sql,
                SqlValue = new SqlValueConfig
                {
                    Query = "SELECT [MaskedValue] FROM [Lookup] WHERE [Key] = @FirstName",
                    ValueHandling = NotFoundValueHandling.KeepValue
                }
            };
            // "Unknown" is not in the lookup table, so ExecuteScalar returns null
            var row = new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "Unknown" };
            var result = provider.GetValue(col, row, null);
            Assert.AreEqual("Unknown", result);
        }
    }
}
