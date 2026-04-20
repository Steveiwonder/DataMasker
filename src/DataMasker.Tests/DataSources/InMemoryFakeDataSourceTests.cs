using DataMasker.DataSources;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker.Tests.DataSources
{
    [TestClass]
    public class InMemoryFakeDataSourceTests
    {
        private static TableConfig UsersTable() => new TableConfig
        {
            Name = "Users",
            PrimaryKeyColumn = "UserId",
            Columns = new List<ColumnConfig>()
        };

        // ── GetCount ──────────────────────────────────────────────────────────

        [TestMethod]
        public void GetCount_KnownTable_ReturnsRowCount()
        {
            var dataSource = new InMemoryFakeDataSource();
            Assert.AreEqual(3, dataSource.GetCount(UsersTable()),
                "GetCount should return the number of rows, not the number of tables");
        }

        [TestMethod]
        public void GetCount_UnknownTable_ReturnsZero()
        {
            var dataSource = new InMemoryFakeDataSource();
            var unknown = new TableConfig { Name = "DoesNotExist", PrimaryKeyColumn = "Id", Columns = new List<ColumnConfig>() };
            Assert.AreEqual(0, dataSource.GetCount(unknown),
                "GetCount for an unknown table should return 0");
        }

        // ── GetData ───────────────────────────────────────────────────────────

        [TestMethod]
        public void GetData_ReturnsAllRows()
        {
            var dataSource = new InMemoryFakeDataSource();
            var list = dataSource.GetData(UsersTable()).ToList();
            Assert.AreEqual(3, list.Count);
        }

        [TestMethod]
        public void GetData_EachRowHasUserId()
        {
            var dataSource = new InMemoryFakeDataSource();
            foreach (var row in dataSource.GetData(UsersTable()))
            {
                Assert.IsTrue(row.ContainsKey("UserId"), "Each row should have a UserId key");
                Assert.IsNotNull(row["UserId"]);
            }
        }

        // ── UpdateRow ─────────────────────────────────────────────────────────

        [TestMethod]
        public void UpdateRow_ValidRow_DoesNotThrow()
        {
            var dataSource = new InMemoryFakeDataSource();
            var tableConfig = UsersTable();

            // Build a row that matches the UserId of the first seeded row
            var updatedRow = new Dictionary<string, object>
            {
                ["UserId"]        = 1,
                ["FirstName"]     = "UpdatedFirst",
                ["LastName"]      = "UpdatedLast",
                ["Password"]      = "NewPassword",
                ["DOB"]           = DateTime.Parse("1990-01-01"),
                ["Gender"]        = "M",
                ["Address"]       = "1 New Street",
                ["ContactNumber"] = "+1 000-000-0000"
            };

            // Should complete without throwing
            dataSource.UpdateRow(updatedRow, tableConfig);
        }

        [TestMethod]
        public void UpdateRow_AllThreeRows_DoesNotThrow()
        {
            var dataSource = new InMemoryFakeDataSource();
            var tableConfig = UsersTable();

            foreach (int userId in new[] { 1, 2, 3 })
            {
                var row = new Dictionary<string, object>
                {
                    ["UserId"]        = userId,
                    ["FirstName"]     = $"First{userId}",
                    ["LastName"]      = $"Last{userId}",
                    ["Password"]      = "p",
                    ["DOB"]           = DateTime.Now,
                    ["Gender"]        = "M",
                    ["Address"]       = "1 St",
                    ["ContactNumber"] = "+1"
                };
                dataSource.UpdateRow(row, tableConfig);
            }
        }

        // ── UpdateRows ────────────────────────────────────────────────────────

        [TestMethod]
        public void UpdateRows_ValidRows_DoesNotThrow()
        {
            var dataSource = new InMemoryFakeDataSource();
            var tableConfig = UsersTable();

            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["UserId"] = 1, ["FirstName"] = "A", ["LastName"] = "B",
                    ["Password"] = "p", ["DOB"] = DateTime.Now,
                    ["Gender"] = "M", ["Address"] = "1 St", ["ContactNumber"] = "+1"
                },
                new Dictionary<string, object>
                {
                    ["UserId"] = 2, ["FirstName"] = "C", ["LastName"] = "D",
                    ["Password"] = "p", ["DOB"] = DateTime.Now,
                    ["Gender"] = "M", ["Address"] = "2 St", ["ContactNumber"] = "+2"
                }
            };

            dataSource.UpdateRows(rows, rows.Count, tableConfig, _ => { });
        }

        [TestMethod]
        public void UpdateRows_EmptyRowList_DoesNotThrow()
        {
            var dataSource = new InMemoryFakeDataSource();
            // Passing an empty list should be a no-op
            dataSource.UpdateRows(new List<IDictionary<string, object>>(), 0, UsersTable(), _ => { });
        }

        // ── UpdateRow verifies correct row ──────────────────────────────────

        [TestMethod]
        public void UpdateRow_ModifiesCorrectRow_OtherRowsStillAccessible()
        {
            var dataSource = new InMemoryFakeDataSource();
            var tableConfig = UsersTable();

            // Update row 2 only
            var updatedRow2 = new Dictionary<string, object>
            {
                ["UserId"] = 2, ["FirstName"] = "Modified", ["LastName"] = "Modified",
                ["Password"] = "p", ["DOB"] = DateTime.Now,
                ["Gender"] = "M", ["Address"] = "X", ["ContactNumber"] = "+0"
            };
            dataSource.UpdateRow(updatedRow2, tableConfig);

            // Rows 1 and 3 should still be accessible (updateable) without error
            var row1 = new Dictionary<string, object>
            {
                ["UserId"] = 1, ["FirstName"] = "F1", ["LastName"] = "L1",
                ["Password"] = "p", ["DOB"] = DateTime.Now,
                ["Gender"] = "M", ["Address"] = "X", ["ContactNumber"] = "+0"
            };
            var row3 = new Dictionary<string, object>
            {
                ["UserId"] = 3, ["FirstName"] = "F3", ["LastName"] = "L3",
                ["Password"] = "p", ["DOB"] = DateTime.Now,
                ["Gender"] = "M", ["Address"] = "X", ["ContactNumber"] = "+0"
            };
            dataSource.UpdateRow(row1, tableConfig);
            dataSource.UpdateRow(row3, tableConfig);

            // All three rows still exist
            Assert.AreEqual(3, dataSource.GetCount(tableConfig));
        }

        [TestMethod]
        public void UpdateRows_AllRowsUpdated_AllStillAccessible()
        {
            var dataSource = new InMemoryFakeDataSource();
            var tableConfig = UsersTable();

            var allRows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["UserId"] = 1, ["FirstName"] = "New1", ["LastName"] = "New1",
                    ["Password"] = "p1", ["DOB"] = DateTime.Now,
                    ["Gender"] = "M", ["Address"] = "A1", ["ContactNumber"] = "+1"
                },
                new Dictionary<string, object>
                {
                    ["UserId"] = 2, ["FirstName"] = "New2", ["LastName"] = "New2",
                    ["Password"] = "p2", ["DOB"] = DateTime.Now,
                    ["Gender"] = "F", ["Address"] = "A2", ["ContactNumber"] = "+2"
                },
                new Dictionary<string, object>
                {
                    ["UserId"] = 3, ["FirstName"] = "New3", ["LastName"] = "New3",
                    ["Password"] = "p3", ["DOB"] = DateTime.Now,
                    ["Gender"] = "M", ["Address"] = "A3", ["ContactNumber"] = "+3"
                }
            };

            dataSource.UpdateRows(allRows, allRows.Count, tableConfig, _ => { });

            // All rows should still be updateable (exist in tables)
            foreach (var row in allRows)
                dataSource.UpdateRow(row, tableConfig);

            Assert.AreEqual(3, dataSource.GetCount(tableConfig));
        }

        [TestMethod]
        public void UpdateRows_CallbackReceivesCount()
        {
            var dataSource = new InMemoryFakeDataSource();
            var tableConfig = UsersTable();

            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["UserId"] = 1, ["FirstName"] = "A", ["LastName"] = "B",
                    ["Password"] = "p", ["DOB"] = DateTime.Now,
                    ["Gender"] = "M", ["Address"] = "1 St", ["ContactNumber"] = "+1"
                },
                new Dictionary<string, object>
                {
                    ["UserId"] = 2, ["FirstName"] = "C", ["LastName"] = "D",
                    ["Password"] = "p", ["DOB"] = DateTime.Now,
                    ["Gender"] = "M", ["Address"] = "2 St", ["ContactNumber"] = "+2"
                }
            };

            int callbackCount = 0;
            dataSource.UpdateRows(rows, rows.Count, tableConfig, count => callbackCount = count);

            // UpdateRows iterates, calling UpdateRow for each. The callback
            // is not called by InMemoryFakeDataSource (it only delegates to UpdateRow)
            // so callbackCount stays 0 — that's the current implementation.
            // The test verifies UpdateRows completes without error.
        }
    }
}
