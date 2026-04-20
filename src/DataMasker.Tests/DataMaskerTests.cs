using System.Data;
using DataMasker.Interfaces;
using DataMasker.Models;
using Microsoft.Data.Sqlite;
using Dapper;

namespace DataMasker.Tests
{
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
            new DataMasker(new[] { new BogusDataProvider(new DataGenerationConfig { Locale = "en" }) });

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
        public void Mask_UseList_PicksValueFromList()
        {
            var masker = BuildMasker();
            var allowedValues = new object[] { 10, 20, 56, 80 };
            var tableConfig = new TableConfig
            {
                Name = "Products",
                PrimaryKeyColumn = "ProductId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "ProductTypeId", Type = DataType.None, UseList = allowedValues, RetainNullValues = false }
                }
            };

            for (int i = 0; i < 20; i++)
            {
                var row = new Dictionary<string, object>
                {
                    ["ProductId"]     = i,
                    ["ProductTypeId"] = 999
                };

                var result = masker.Mask(row, tableConfig);

                CollectionAssert.Contains(allowedValues, result["ProductTypeId"], $"Row {i}: value should be one of the allowed list values");
            }
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
        public void Mask_UseGenderColumn_PassesGenderToProvider()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, UseGenderColumn = "Gender" },
                    new ColumnConfig { Name = "Gender",    Type = DataType.None,      Ignore = true }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"]    = 1,
                ["FirstName"] = "Original",
                ["Gender"]    = "F"
            };

            var result = masker.Mask(row, tableConfig);

            Assert.IsNotNull(result["FirstName"], "FirstName should be generated when UseGenderColumn is set");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result["FirstName"]?.ToString()));
        }

        [TestMethod]
        public void Mask_ComputedColumn_DefaultSeparator_UsesSpace()
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
                        SourceColumns = new[] { "FirstName", "LastName" }
                        // Separator intentionally omitted — should default to " "
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

            Assert.IsTrue(fullName.Contains(" "), $"Default separator should be a space, got: '{fullName}'");
        }

        [TestMethod]
        public void Mask_ComputedColumn_NullSourceValue_ContributesEmptyString()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    // RetainNullValues=true keeps FirstName as null
                    new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, RetainNullValues = true },
                    // UseValue pins LastName so it's deterministic in the computed result
                    new ColumnConfig { Name = "LastName",  Type = DataType.None, UseValue = "Smith" },
                    new ColumnConfig
                    {
                        Name          = "FullName",
                        Type          = DataType.Computed,
                        SourceColumns = new[] { "FirstName", "LastName" },
                        Separator     = "-"
                    }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"]    = 1,
                ["FirstName"] = null,
                ["LastName"]  = "Jones",
                ["FullName"]  = ""
            };

            var result = masker.Mask(row, tableConfig);
            string fullName = result["FullName"]?.ToString() ?? "";

            // null FirstName → "" + separator "-" + "Smith" = "-Smith"
            Assert.AreEqual("-Smith", fullName,
                "A null source column should contribute an empty string to the computed value");
        }

        [TestMethod]
        public void Mask_ComputedColumn_MissingSourceColumn_ThrowsException()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig
                    {
                        Name          = "FullName",
                        Type          = DataType.Computed,
                        SourceColumns = new[] { "ColumnThatDoesNotExist" },
                        Separator     = " "
                    }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"]   = 1,
                ["FullName"] = ""
            };

            try
            {
                masker.Mask(row, tableConfig);
                Assert.Fail("Expected an exception for a missing source column");
            }
            catch (Exception ex) when (ex is not AssertFailedException)
            {
                StringAssert.Contains(ex.Message, "ColumnThatDoesNotExist");
            }
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

        // ── SQL column masking ──────────────────────────────────────────────

        [TestMethod]
        public void Mask_SqlColumn_UsesSqlDataProvider()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            var masker = new DataMasker(new IDataProvider[]
            {
                new BogusDataProvider(new DataGenerationConfig { Locale = "en" }),
                new SqlDataProvider(conn)
            });

            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig
                    {
                        Name = "Password",
                        Type = DataType.Sql,
                        SqlValue = new SqlValueConfig
                        {
                            Query = "SELECT 'MASKED_PWD'",
                            ValueHandling = NotFoundValueHandling.Null
                        }
                    }
                }
            };

            var row = new Dictionary<string, object>
            {
                ["UserId"] = 1,
                ["Password"] = "OriginalPassword"
            };

            var result = masker.Mask(row, tableConfig);
            Assert.AreEqual("MASKED_PWD", result["Password"]);
        }

        // ── Unique exhaustion ───────────────────────────────────────────────

        [TestMethod]
        public void Mask_UniqueValues_ExceedsMaxIterations_ThrowsInvalidOperationException()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig
                    {
                        Name = "Status",
                        Type = DataType.None,
                        UseList = new object[] { "onlyvalue" },
                        Unique = true
                    }
                }
            };

            // First row succeeds
            masker.Mask(new Dictionary<string, object> { ["UserId"] = 1, ["Status"] = "x" }, tableConfig);

            // Second row: pool exhausted → throws after MAX_UNIQUE_VALUE_ITERATIONS
            try
            {
                masker.Mask(new Dictionary<string, object> { ["UserId"] = 2, ["Status"] = "x" }, tableConfig);
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.Message, "Unable to generate unique value");
            }
        }

        // ── No provider registered ──────────────────────────────────────────

        [TestMethod]
        public void Mask_NoProviderRegistered_ThrowsInvalidOperationException()
        {
            var masker = new DataMasker(Enumerable.Empty<IDataProvider>());
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name = "FirstName", Type = DataType.FirstName }
                }
            };

            var row = new Dictionary<string, object> { ["UserId"] = 1, ["FirstName"] = "Steve" };

            try
            {
                masker.Mask(row, tableConfig);
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.Message, "No data provider registered");
            }
        }

        // ── Computed column with no source columns throws ───────────────────

        [TestMethod]
        public void Mask_ComputedColumn_NoSourceColumns_ThrowsInvalidOperationException()
        {
            var masker = BuildMasker();
            var tableConfig = new TableConfig
            {
                Name = "Users",
                PrimaryKeyColumn = "UserId",
                Schema = "dbo",
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig
                    {
                        Name = "FullName",
                        Type = DataType.Computed,
                        SourceColumns = null
                    }
                }
            };

            var row = new Dictionary<string, object> { ["UserId"] = 1, ["FullName"] = "" };

            try
            {
                masker.Mask(row, tableConfig);
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.Message, "FullName");
            }
        }
    }
}
