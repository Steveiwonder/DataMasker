using System.Collections.Generic;
using DataMasker.Models;

namespace DataMasker.Tests
{
    [TestClass]
    public class ExtensionsTests
    {
        // ── GetSelectColumns ─────────────────────────────────────────────────

        [TestMethod]
        public void GetSelectColumns_AlwaysIncludesPrimaryKey()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName },
                new ColumnConfig { Name = "LastName",  Type = DataType.LastName  }
            };

            string sql = columns.GetSelectColumns("UserId");

            Assert.IsTrue(sql.Contains("[UserId]"), $"Expected [UserId] in: {sql}");
        }

        [TestMethod]
        public void GetSelectColumns_IncludesAllColumns()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName },
                new ColumnConfig { Name = "LastName",  Type = DataType.LastName  },
                new ColumnConfig { Name = "DOB",       Type = DataType.DateOfBirth }
            };

            string sql = columns.GetSelectColumns("UserId");

            Assert.IsTrue(sql.Contains("[FirstName]"), $"Expected [FirstName] in: {sql}");
            Assert.IsTrue(sql.Contains("[LastName]"),  $"Expected [LastName] in: {sql}");
            Assert.IsTrue(sql.Contains("[DOB]"),       $"Expected [DOB] in: {sql}");
        }

        [TestMethod]
        public void GetSelectColumns_PrimaryKeyAppearsFirst()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName }
            };

            string sql = columns.GetSelectColumns("UserId");

            int pkIndex  = sql.IndexOf("[UserId]");
            int colIndex = sql.IndexOf("[FirstName]");
            Assert.IsTrue(pkIndex < colIndex, $"Primary key should appear before other columns in: {sql}");
        }

        [TestMethod]
        public void GetSelectColumns_EmptyColumnList_StillReturnsPrimaryKey()
        {
            var columns = new List<ColumnConfig>();
            string sql = columns.GetSelectColumns("UserId");
            Assert.AreEqual("[UserId]", sql.Trim());
        }

        // ── GetUpdateColumns ─────────────────────────────────────────────────

        [TestMethod]
        public void GetUpdateColumns_IncludesNonIgnoredColumns()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, Ignore = false },
                new ColumnConfig { Name = "LastName",  Type = DataType.LastName,  Ignore = false }
            };

            string sql = columns.GetUpdateColumns();

            Assert.IsTrue(sql.Contains("[FirstName]"), $"Expected [FirstName] in: {sql}");
            Assert.IsTrue(sql.Contains("[LastName]"),  $"Expected [LastName] in: {sql}");
        }

        [TestMethod]
        public void GetUpdateColumns_ExcludesIgnoredColumns()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, Ignore = false },
                new ColumnConfig { Name = "Gender",    Type = DataType.None,      Ignore = true  }
            };

            string sql = columns.GetUpdateColumns();

            Assert.IsTrue(sql.Contains("[FirstName]"),  $"Expected [FirstName] in: {sql}");
            Assert.IsFalse(sql.Contains("[Gender]"),    $"Ignored column [Gender] should not appear in: {sql}");
        }

        [TestMethod]
        public void GetUpdateColumns_AssignsParameterPlaceholders()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName }
            };

            string sql = columns.GetUpdateColumns();

            // Should contain something like: [FirstName] = @FirstName
            Assert.IsTrue(sql.Contains("@FirstName"), $"Expected @FirstName param in: {sql}");
        }

        [TestMethod]
        public void GetUpdateColumns_WithParamPrefix_PrependsPrefixToParams()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "FirstName", Type = DataType.FirstName }
            };

            string sql = columns.GetUpdateColumns(paramPrefix: "p_");

            Assert.IsTrue(sql.Contains("@p_FirstName"), $"Expected @p_FirstName param in: {sql}");
        }

        [TestMethod]
        public void GetUpdateColumns_ColumnNameWithSpaces_IsSafelyBracketed()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "First Name", Type = DataType.FirstName }
            };

            string sql = columns.GetUpdateColumns();

            Assert.IsTrue(sql.Contains("[First Name]"), $"Expected bracketed [First Name] in: {sql}");
            Assert.IsTrue(sql.Contains("@First_Name"),  $"Expected underscored @First_Name param in: {sql}");
        }

        [TestMethod]
        public void GetUpdateColumns_AllColumnsIgnored_ReturnsEmptyString()
        {
            var columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "Gender", Type = DataType.None, Ignore = true }
            };

            string sql = columns.GetUpdateColumns();

            Assert.AreEqual(string.Empty, sql.Trim());
        }
    }

    [TestClass]
    public class UtilsExtendedTests
    {
        [TestMethod]
        public void MakeParamNameSafe_ReplacesSpacesWithUnderscores()
        {
            string result = Utils.Utils.MakeParamNameSafe("First Name");
            Assert.AreEqual("First_Name", result);
        }

        [TestMethod]
        public void MakeParamNameSafe_ReplacesHyphensWithUnderscores()
        {
            string result = Utils.Utils.MakeParamNameSafe("date-of-birth");
            Assert.AreEqual("date_of_birth", result);
        }

        [TestMethod]
        public void MakeParamNameSafe_NoSpecialChars_Unchanged()
        {
            string result = Utils.Utils.MakeParamNameSafe("FirstName");
            Assert.AreEqual("FirstName", result);
        }

        [TestMethod]
        public void MakeColumnNameSafe_AddsSquareBrackets()
        {
            string result = Utils.Utils.MakeColumnNameSafe("UserId");
            Assert.AreEqual("[UserId]", result);
        }

        [TestMethod]
        public void MakeColumnNameSafe_AlreadyBracketed_NoChange()
        {
            string result = Utils.Utils.MakeColumnNameSafe("[UserId]");
            Assert.AreEqual("[UserId]", result);
        }

        [TestMethod]
        public void MakeParamNamesSafe_ConvertsAllKeys()
        {
            var input = new Dictionary<string, object>
            {
                ["First Name"] = "Steve",
                ["date-of-birth"] = "1990-01-01",
                ["NormalKey"] = 42
            };

            var result = Utils.Utils.MakeParamNamesSafe(input);

            Assert.IsTrue(result.ContainsKey("First_Name"),    "Expected 'First_Name'");
            Assert.IsTrue(result.ContainsKey("date_of_birth"), "Expected 'date_of_birth'");
            Assert.IsTrue(result.ContainsKey("NormalKey"),     "Expected 'NormalKey' unchanged");
        }

        [TestMethod]
        public void MakeParamNamesSafe_PreservesValues()
        {
            var input = new Dictionary<string, object>
            {
                ["First Name"] = "Steve"
            };

            var result = Utils.Utils.MakeParamNamesSafe(input);

            Assert.AreEqual("Steve", result["First_Name"]);
        }
    }

    [TestClass]
    public class DataSourceProviderTests
    {
        [TestMethod]
        public void Provide_InMemoryFake_ReturnsInMemoryFakeDataSource()
        {
            var source = DataSourceProvider.Provide(DataSourceType.InMemoryFake);
            Assert.IsInstanceOfType(source, typeof(DataSources.InMemoryFakeDataSource));
        }

        [TestMethod]
        public void Provide_SqlServer_ReturnsSqlDataSource()
        {
            // Use JObject to mirror what Newtonsoft.Json produces when the config is loaded from JSON
            var config = new Models.DataSourceConfig
            {
                Type   = DataSourceType.SqlServer,
                Config = Newtonsoft.Json.Linq.JObject.Parse(@"{ ""connectionString"": ""Server=.;Database=test;"" }")
            };
            var source = DataSourceProvider.Provide(DataSourceType.SqlServer, config);
            Assert.IsInstanceOfType(source, typeof(DataSources.SqlDataSource));
        }

        [TestMethod]
        public void Provide_UnknownType_ThrowsArgumentOutOfRangeException()
        {
            try
            {
                DataSourceProvider.Provide((DataSourceType)999);
                Assert.Fail("Expected ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }
    }
}
