using Bogus.DataSets;
using DataMasker.DataSources;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker.Tests
{
    [TestClass]
    public class UniqueValueTests
    {
        /// <summary>
        /// Hands out values from a fixed sequence so collisions are deterministic.
        /// </summary>
        private sealed class SequenceProvider : IDataProvider
        {
            private readonly Queue<object> _values;
            public int Calls { get; private set; }

            public SequenceProvider(params object[] values) => _values = new Queue<object>(values);

            public bool CanProvide(DataType dataType) => dataType != DataType.Sql && dataType != DataType.Computed;

            public object GetValue(ColumnConfig columnConfig, IDictionary<string, object> obj, Name.Gender? gender)
            {
                Calls++;
                return _values.Count > 0 ? _values.Dequeue() : "exhausted";
            }
        }

        private static TableConfig Table(ColumnConfig column) => new TableConfig
        {
            Name = "Users",
            Schema = "dbo",
            PrimaryKeyColumn = "UserId",
            Columns = new List<ColumnConfig> { column }
        };

        private static Dictionary<string, object> Row(object value) => new Dictionary<string, object> { ["UserId"] = 1, ["Email"] = value };

        [TestMethod]
        public void Mask_UniqueWithLocalValueMappings_SameInputGetsSameOutputWithoutThrowing()
        {
            var column = new ColumnConfig { Name = "Email", Type = DataType.FirstName, Unique = true, UseLocalValueMappings = true };
            var masker = new DataMasker(new IDataProvider[] { new BogusDataProvider(new DataGenerationConfig()) });
            var table = Table(column);

            var first = masker.Mask(Row("steve@example.com"), table)["Email"];
            var second = masker.Mask(Row("steve@example.com"), table)["Email"];
            var other = masker.Mask(Row("jane@example.com"), table)["Email"];

            Assert.AreEqual(first, second, "the same source value must map to the same replacement");
            Assert.AreNotEqual(first, other, "different source values must get different unique replacements");
        }

        [TestMethod]
        public void Mask_UniqueWithGlobalValueMappings_SharedAcrossTables()
        {
            var column = new ColumnConfig { Name = "Email", Type = DataType.FirstName, Unique = true, UseGlobalValueMappings = true };
            var masker = new DataMasker(new IDataProvider[] { new BogusDataProvider(new DataGenerationConfig()) });
            var users = Table(column);
            var orders = new TableConfig { Name = "Orders", Schema = "dbo", PrimaryKeyColumn = "UserId", Columns = new List<ColumnConfig> { column } };

            var fromUsers = masker.Mask(Row("steve@example.com"), users)["Email"];
            var fromOrders = masker.Mask(Row("steve@example.com"), orders)["Email"];

            Assert.AreEqual(fromUsers, fromOrders);
        }

        [TestMethod]
        public void Mask_UniqueWithMappings_RetriesWhenGeneratedValueCollides()
        {
            // "a" is handed out for both rows; the second row must retry and receive "b".
            var provider = new SequenceProvider("a", "a", "b");
            var column = new ConfigWithMappings { Name = "Email", Type = DataType.FirstName, Unique = true, UseLocalValueMappings = true };
            var masker = new DataMasker(new IDataProvider[] { provider });
            var table = Table(column);

            var first = masker.Mask(Row("x"), table)["Email"];
            var second = masker.Mask(Row("y"), table)["Email"];

            Assert.AreEqual("a", first);
            Assert.AreEqual("b", second);
            Assert.AreEqual(3, provider.Calls);
        }

        private sealed class ConfigWithMappings : ColumnConfig { }

        [TestMethod]
        public void Mask_UniqueWithConfiguredValueMapping_UsesConfiguredReplacement()
        {
            var provider = new SequenceProvider("generated");
            var column = new ColumnConfig
            {
                Name = "Email",
                Type = DataType.FirstName,
                Unique = true,
                UseLocalValueMappings = true,
                ValueMappings = new Dictionary<object, object> { ["steve@example.com"] = "fixed@example.com" }
            };
            var masker = new DataMasker(new IDataProvider[] { provider });
            var table = Table(column);

            Assert.AreEqual("fixed@example.com", masker.Mask(Row("steve@example.com"), table)["Email"]);
            Assert.AreEqual("generated", masker.Mask(Row("other@example.com"), table)["Email"]);
        }

        [TestMethod]
        public void SeedUniqueValues_GeneratedValuesAvoidSeededOnes()
        {
            var provider = new SequenceProvider("taken1", "taken2", "free");
            var column = new ColumnConfig { Name = "Email", Type = DataType.FirstName, Unique = true };
            var masker = new DataMasker(new IDataProvider[] { provider });
            var table = Table(column);

            masker.SeedUniqueValues("Users", "Email", new object[] { "taken1", null!, "taken2" });
            var result = masker.Mask(Row("x"), table)["Email"];

            Assert.AreEqual("free", result);
            Assert.AreEqual(3, provider.Calls);
        }

        [TestMethod]
        public void SeedUniqueValues_FromDataSource_SeedsEveryUniqueColumn()
        {
            var dataSource = new InMemoryFakeDataSource();
            var provider = new SequenceProvider("Steve", "Jane", "Fresh");
            var column = new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, Unique = true };
            var table = new TableConfig { Name = "Users", PrimaryKeyColumn = "UserId", Columns = new List<ColumnConfig> { column } };
            var masker = new DataMasker(new IDataProvider[] { provider });

            masker.SeedUniqueValues(dataSource, table);
            var row = dataSource.GetData(table).First();
            var result = masker.Mask(row, table)["FirstName"];

            Assert.AreEqual("Fresh", result, "existing first names should have been seeded and skipped");
        }

        [TestMethod]
        public void SeedUniqueValues_NullValues_Ignored()
        {
            var masker = new DataMasker(new IDataProvider[] { new SequenceProvider("v") });
            masker.SeedUniqueValues("Users", "Email", null!);
            masker.SeedUniqueValues("Users", "Email", new object[] { null! });
            Assert.AreEqual("v", masker.Mask(Row("x"), Table(new ColumnConfig { Name = "Email", Type = DataType.FirstName, Unique = true }))["Email"]);
        }

        [TestMethod]
        public void Mask_MissingColumnInRow_ThrowsDescriptiveError()
        {
            var masker = new DataMasker(new IDataProvider[] { new BogusDataProvider(new DataGenerationConfig()) });
            var table = Table(new ColumnConfig { Name = "Email", Type = DataType.FirstName });

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => masker.Mask(new Dictionary<string, object> { ["UserId"] = 1 }, table));
            StringAssert.Contains(ex.Message, "Email");
        }

        [TestMethod]
        public void Mask_MissingGenderColumnInRow_ThrowsDescriptiveError()
        {
            var masker = new DataMasker(new IDataProvider[] { new BogusDataProvider(new DataGenerationConfig()) });
            var table = Table(new ColumnConfig { Name = "Email", Type = DataType.FirstName, UseGenderColumn = "Gender" });

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => masker.Mask(Row("x"), table));
            StringAssert.Contains(ex.Message, "Gender");
        }
    }
}
