using DataMasker.Models;

namespace DataMasker.Tests.Extensions
{
    [TestClass]
    public class ExtensionsTests
    {
        // ── GetSelectColumns ──────────────────────────────────────────────────

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

        // ── GetUpdateColumns ──────────────────────────────────────────────────

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
}
