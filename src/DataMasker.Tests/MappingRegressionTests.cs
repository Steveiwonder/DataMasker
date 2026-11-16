using Bogus.DataSets;
using DataMasker.Interfaces;
using DataMasker.Models;

namespace DataMasker.Tests;

[TestClass]
public class MappingRegressionTests
{
    private sealed class SequenceProvider : IDataProvider
    {
        private readonly Queue<object> _values;
        public int Calls { get; private set; }
        public SequenceProvider(params object[] values) => _values = new Queue<object>(values);
        public bool CanProvide(DataType type) => true;
        public object GetValue(ColumnConfig column, IDictionary<string, object> row, Name.Gender? gender)
        {
            Calls++;
            return _values.Dequeue();
        }
    }

    private static ColumnConfig Column(bool unique = true, bool global = true) => new()
    {
        Name = "Email", Type = DataType.StringFormat, Unique = unique,
        UseGlobalValueMappings = global, StringFormatPattern = "replacement"
    };
    private static TableConfig Table(string name, ColumnConfig column, string schema = "dbo") => new()
    {
        Name = name, Schema = schema, PrimaryKeyColumn = "Id", Columns = new List<ColumnConfig> { column }
    };
    private static Dictionary<string, object> Row(string value) => new() { ["Id"] = 1, ["Email"] = value };

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GlobalMappings_UniqueAndNonUniqueConsumersShareReplacement(bool uniqueFirst)
    {
        var provider = new SequenceProvider("first", "second");
        var masker = new DataMasker(new[] { provider });
        var first = masker.Mask(Row("original"), Table("A", Column(uniqueFirst)))["Email"];
        var second = masker.Mask(Row("original"), Table("B", Column(!uniqueFirst)))["Email"];
        Assert.AreEqual(first, second);
        Assert.AreEqual(1, provider.Calls);
    }

    [TestMethod]
    public void GlobalMappings_ReusedReplacementIsReservedInDestination()
    {
        var provider = new SequenceProvider("a", "a", "b");
        var masker = new DataMasker(new[] { provider });
        masker.Mask(Row("x"), Table("A", Column()));
        Assert.AreEqual("a", masker.Mask(Row("x"), Table("B", Column()))["Email"]);
        Assert.AreEqual("b", masker.Mask(Row("y"), Table("B", Column()))["Email"]);
        Assert.AreEqual(3, provider.Calls);
    }

    [TestMethod]
    public void GlobalMappings_SeededDestinationCollisionFailsWithoutChangingMapping()
    {
        var masker = new DataMasker(new[] { new SequenceProvider("a") });
        masker.Mask(Row("x"), Table("A", Column()));
        masker.SeedUniqueValues("B", "Email", new object[] { "a" });
        Assert.ThrowsExactly<InvalidOperationException>(() => masker.Mask(Row("x"), Table("B", Column())));
        Assert.AreEqual("a", masker.Mask(Row("x"), Table("A", Column()))["Email"]);
    }

    [TestMethod]
    public void LocalMappings_AreIsolatedBySchema()
    {
        var provider = new SequenceProvider("a", "b");
        var masker = new DataMasker(new[] { provider });
        var c = Column(global: false); c.UseLocalValueMappings = true;
        Assert.AreEqual("a", masker.Mask(Row("x"), Table("Users", c, "one"))["Email"]);
        Assert.AreEqual("b", masker.Mask(Row("x"), Table("Users", c, "two"))["Email"]);
    }

    [TestMethod]
    public void UniqueSets_AreIsolatedBySchema()
    {
        var provider = new SequenceProvider("a", "a");
        var masker = new DataMasker(new[] { provider });
        Assert.AreEqual("a", masker.Mask(Row("x"), Table("Users", Column(global: false), "one"))["Email"]);
        Assert.AreEqual("a", masker.Mask(Row("y"), Table("Users", Column(global: false), "two"))["Email"]);
    }

    [TestMethod]
    [DataRow(DataType.Computed)]
    [DataRow(DataType.Sql)]
    public void UniqueUnsupportedGeneration_FailsBeforeMutatingRow(DataType type)
    {
        var normal = Column(unique: false); var unsupported = new ColumnConfig { Name = "Other", Type = type, Unique = true };
        var table = Table("Users", normal); table.Columns.Add(unsupported);
        var row = Row("original"); row["Other"] = "untouched";
        var provider = new SequenceProvider("replacement");
        Assert.ThrowsExactly<InvalidOperationException>(() => new DataMasker(new[] { provider }).Mask(row, table));
        Assert.AreEqual("original", row["Email"]);
        Assert.AreEqual(0, provider.Calls);
    }

    [TestMethod]
    public void EmptyConstant_ClearsValue()
    {
        var provider = new BogusDataProvider(new DataGenerationConfig());
        Assert.AreEqual("", provider.GetValue(new ColumnConfig { Name = "Email", Type = DataType.None, UseValue = "" }, Row("secret"), null));
    }

    [TestMethod]
    public void Unique_FinalAllowedAttemptCanSucceed()
    {
        var provider = new SequenceProvider(Enumerable.Repeat<object>("taken", 4999).Append("free").ToArray());
        var masker = new DataMasker(new[] { provider });
        masker.SeedUniqueValues("Users", "Email", new object[] { "taken" });
        Assert.AreEqual("free", masker.Mask(Row("x"), Table("Users", Column(global: false)))["Email"]);
        Assert.AreEqual(5000, provider.Calls);
    }

    [TestMethod]
    public void ConfiguredUniqueMapping_CollisionFails()
    {
        var column = Column(global: false); column.UseLocalValueMappings = true;
        column.ValueMappings = new Dictionary<object, object> { ["x"] = "a", ["y"] = "a" };
        var masker = new DataMasker(new IDataProvider[] { new SequenceProvider() });
        masker.Mask(Row("x"), Table("Users", column));
        Assert.ThrowsExactly<InvalidOperationException>(() => masker.Mask(Row("y"), Table("Users", column)));
    }
    private sealed class CaseInsensitiveSource : IDataSource, IUniqueValueComparerSource
    {
        private readonly object[] _values;
        public CaseInsensitiveSource(params object[] values) => _values = values;
        public IEqualityComparer<object> GetValueComparer(TableConfig table, string column) => new TextComparer();
        public IEnumerable<object> GetDistinctValues(TableConfig table, string column) => _values;
        public IEnumerable<IDictionary<string, object>> GetData(TableConfig table) => throw new NotSupportedException();
        public int GetCount(TableConfig table) => throw new NotSupportedException();
        public void UpdateRow(IDictionary<string, object> row, TableConfig table) => throw new NotSupportedException();
        public void UpdateRows(IEnumerable<IDictionary<string, object>> rows, int count, TableConfig table, Action<int>? callback = null) => throw new NotSupportedException();
    }
    private sealed class TextComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => StringComparer.OrdinalIgnoreCase.Equals(x as string, y as string);
        public int GetHashCode(object value) => StringComparer.OrdinalIgnoreCase.GetHashCode((string)value);
    }

    [TestMethod]
    public void SourceComparer_RejectsCollidingSeedAndGeneratedValue()
    {
        var provider = new SequenceProvider("TAKEN", "free");
        var masker = new DataMasker(new[] { provider });
        var table = Table("Users", Column(global: false), "custom");
        masker.SeedUniqueValues(new CaseInsensitiveSource("taken"), table);
        Assert.AreEqual("free", masker.Mask(Row("x"), table)["Email"]);
        Assert.AreEqual(2, provider.Calls);
    }

    [TestMethod]
    public void SourceComparer_ReservationOwnershipSupportsRepeatedGlobalReferences()
    {
        var masker = new DataMasker(new[] { new SequenceProvider("replacement") });
        var table = Table("Users", Column());
        masker.SeedUniqueValues(new CaseInsensitiveSource(), table);
        Assert.AreEqual("replacement", masker.Mask(Row("x"), table)["Email"]);
        Assert.AreEqual("replacement", masker.Mask(Row("x"), table)["Email"]);
    }

    [TestMethod]
    public void OrdinaryColumn_RetainEmptyPrecedesConfiguredMapping()
    {
        var column = Column(unique: false, global: false);
        column.UseLocalValueMappings = true;
        column.RetainEmptyStringValues = true;
        column.ValueMappings = new Dictionary<object, object> { [" "] = "replacement" };
        var masker = new DataMasker(new[] { new BogusDataProvider(new DataGenerationConfig()) });
        Assert.AreEqual(" ", masker.Mask(Row(" "), Table("Users", column))["Email"]);
    }

    [TestMethod]
    public void OrdinaryColumn_ExplicitValueAndListPrecedeExistingGlobalMapping()
    {
        var masker = new DataMasker(new[] { new BogusDataProvider(new DataGenerationConfig()) });
        Assert.AreEqual("replacement", masker.Mask(Row("original"), Table("A", Column()))["Email"]);
        var constant = Column(unique: false); constant.UseValue = "constant";
        var list = Column(unique: false); list.UseList = new object[] { "list" };
        Assert.AreEqual("constant", masker.Mask(Row("original"), Table("B", constant))["Email"]);
        Assert.AreEqual("list", masker.Mask(Row("original"), Table("C", list))["Email"]);
    }

    [TestMethod]
    public void SqlComparer_EqualityAndHashAgreeOnCaseAccentAndTrailingSpace()
    {
        var comparer = new global::DataMasker.DataSources.SqlCollationComparer(1033, 3, "Latin1_General_100_CI_AI");
        Assert.IsTrue(comparer.Equals("café", "CAFE "));
        Assert.AreEqual(comparer.GetHashCode("café"), comparer.GetHashCode("CAFE "));
        var binary = new global::DataMasker.DataSources.SqlCollationComparer(1033, 0, "Latin1_General_100_BIN2");
        Assert.IsFalse(binary.Equals("a", "A"));
        Assert.IsTrue(binary.Equals("a", "a "));
    }

    [TestMethod]
    [DataRow(DataType.Sql)]
    [DataRow(DataType.Computed)]
    public void ComputedSourceMaskedInLaterPhase_FailsBeforeAnyMutation(DataType sourceType)
    {
        var table = Table("Users", Column(unique: false, global: false));
        table.Columns.Add(new ColumnConfig { Name = "Derived", Type = DataType.Computed, SourceColumns = new[] { "Late" } });
        table.Columns.Add(new ColumnConfig { Name = "Late", Type = sourceType, SourceColumns = new[] { "Email" } });
        var row = Row("secret"); row["Derived"] = "old"; row["Late"] = "sensitive";
        var provider = new SequenceProvider("replacement");
        Assert.ThrowsExactly<InvalidOperationException>(() => new DataMasker(new[] { provider }).Mask(row, table));
        Assert.AreEqual("secret", row["Email"]);
        Assert.AreEqual("old", row["Derived"]);
        Assert.AreEqual(0, provider.Calls);
    }

    [TestMethod]
    public void ComputedSelfReference_FailsValidation()
    {
        var column = new ColumnConfig { Name = "Email", Type = DataType.Computed, SourceColumns = new[] { "Email" } };
        Assert.ThrowsExactly<InvalidOperationException>(() => DataMasker.ValidateTable(Table("Users", column)));
    }

    [TestMethod]
    public void ComputedDependenciesEarlierInOrder_UseMaskedValues()
    {
        var table = Table("Users", Column(unique: false, global: false));
        table.Columns.Add(new ColumnConfig { Name = "First", Type = DataType.Computed, SourceColumns = new[] { "Email" } });
        table.Columns.Add(new ColumnConfig { Name = "Second", Type = DataType.Computed, SourceColumns = new[] { "First" } });
        var row = Row("secret"); row["First"] = "old"; row["Second"] = "old";
        new DataMasker(new[] { new SequenceProvider("safe") }).Mask(row, table);
        Assert.AreEqual("safe", row["First"]);
        Assert.AreEqual("safe", row["Second"]);
    }

    [TestMethod]
    public void ConfiguredUniqueMapping_DoesNotRequireGenerationProvider()
    {
        var column = Column(global: false); column.UseLocalValueMappings = true;
        column.ValueMappings = new Dictionary<object, object> { ["original"] = "mapped" };
        var masker = new DataMasker(Array.Empty<IDataProvider>());
        var table = Table("Users", column);
        Assert.AreEqual("mapped", masker.Mask(Row("original"), table)["Email"]);
        Assert.AreEqual("mapped", masker.Mask(Row("original"), table)["Email"]);
    }

    [TestMethod]
    public void InMemoryMapping_UnnamedTablePreservesMappingIdentity()
    {
        var column = Column(global: false); column.UseLocalValueMappings = true;
        var table = new TableConfig { Columns = new List<ColumnConfig> { column } };
        var masker = new DataMasker(new[] { new SequenceProvider("mapped") });
        Assert.AreEqual("mapped", masker.Mask(Row("original"), table)["Email"]);
        Assert.AreEqual("mapped", masker.Mask(Row("original"), table)["Email"]);
    }

    [TestMethod]
    public void SourceComparer_ReplacesDefaultComparerForEarlierSupplementalSeeds()
    {
        var masker = new DataMasker(new[] { new SequenceProvider("TAKEN", "free") });
        var table = Table("Users", Column(global: false));
        masker.SeedUniqueValues("Users", "Email", new object[] { "taken" });
        masker.SeedUniqueValues(new CaseInsensitiveSource(), table);
        Assert.AreEqual("free", masker.Mask(Row("original"), table)["Email"]);
    }

}
