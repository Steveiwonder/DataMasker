using System;
using System.Collections.Generic;
using System.Linq;
using DataMasker.Models;

namespace DataMasker.Tests
{
    [TestClass]
    public class BogusDataProviderCanProvideTests
    {
        private static readonly DataType[] SupportedTypes = new[]
        {
            DataType.FirstName, DataType.LastName, DataType.DateOfBirth,
            DataType.Rant,      DataType.Lorem,    DataType.StringFormat,
            DataType.FullAddress, DataType.PhoneNumber, DataType.Bogus,
            DataType.None
        };

        private static BogusDataProvider Provider() =>
            new BogusDataProvider(new DataGenerationConfig { Locale = "en" });

        [TestMethod]
        public void CanProvide_SupportedTypes_ReturnsTrue()
        {
            var provider = Provider();
            foreach (var type in SupportedTypes)
                Assert.IsTrue(provider.CanProvide(type), $"Expected CanProvide=true for {type}");
        }

        [TestMethod]
        public void CanProvide_Sql_ReturnsFalse()
        {
            Assert.IsFalse(Provider().CanProvide(DataType.Sql));
        }

        [TestMethod]
        public void CanProvide_Computed_ReturnsFalse()
        {
            Assert.IsFalse(Provider().CanProvide(DataType.Computed));
        }
    }

    [TestClass]
    public class BogusDataProviderGetValueTests
    {
        private static BogusDataProvider Provider() =>
            new BogusDataProvider(new DataGenerationConfig { Locale = "en" });

        private static IDictionary<string, object> Row(string colName, object value) =>
            new Dictionary<string, object> { [colName] = value };

        private static ColumnConfig Col(string name, DataType type) =>
            new ColumnConfig { Name = name, Type = type };

        // ── basic data types ─────────────────────────────────────────────────

        [TestMethod]
        public void GetValue_FirstName_ReturnsNonEmptyString()
        {
            var result = Provider().GetValue(Col("FirstName", DataType.FirstName), Row("FirstName", "Steve"), null);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)result));
        }

        [TestMethod]
        public void GetValue_LastName_ReturnsNonEmptyString()
        {
            var result = Provider().GetValue(Col("LastName", DataType.LastName), Row("LastName", "Smith"), null);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)result));
        }

        [TestMethod]
        public void GetValue_DateOfBirth_ReturnsDateTime()
        {
            var result = Provider().GetValue(Col("DOB", DataType.DateOfBirth), Row("DOB", DateTime.Now), null);
            Assert.IsInstanceOfType(result, typeof(DateTime));
        }

        [TestMethod]
        public void GetValue_DateOfBirth_WithMinMax_RespectsRange()
        {
            var col = new ColumnConfig
            {
                Name = "DOB",
                Type = DataType.DateOfBirth,
                Min  = "1990-01-01",
                Max  = "2000-12-31"
            };
            var result = (DateTime)Provider().GetValue(col, Row("DOB", DateTime.Now), null)!;
            Assert.IsTrue(result >= new DateTime(1990, 1, 1), $"DOB {result} is before Min");
            Assert.IsTrue(result <= new DateTime(2000, 12, 31), $"DOB {result} is after Max");
        }

        [TestMethod]
        public void GetValue_Lorem_ReturnsNonEmptyString()
        {
            var col = new ColumnConfig { Name = "Bio", Type = DataType.Lorem, Min = "3", Max = "8" };
            var result = Provider().GetValue(col, Row("Bio", "old bio"), null);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)result));
        }

        [TestMethod]
        public void GetValue_Rant_ReturnsNonEmptyStringArray()
        {
            // Bogus Faker.Rant.Reviews returns string[], one element per line
            var col = new ColumnConfig { Name = "Review", Type = DataType.Rant, Max = "2" };
            var result = Provider().GetValue(col, Row("Review", "old review"), null);
            Assert.IsInstanceOfType(result, typeof(string[]));
            var lines = (string[])result;
            Assert.IsTrue(lines.Length > 0, "Rant should return at least one review line");
            Assert.IsFalse(string.IsNullOrWhiteSpace(lines[0]));
        }

        [TestMethod]
        public void GetValue_FullAddress_ReturnsNonEmptyString()
        {
            var result = Provider().GetValue(Col("Address", DataType.FullAddress), Row("Address", "old address"), null);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)result));
        }

        [TestMethod]
        public void GetValue_PhoneNumber_ReturnsFormattedString()
        {
            var col = new ColumnConfig { Name = "Phone", Type = DataType.PhoneNumber, StringFormatPattern = "+1##########" };
            var result = (string)Provider().GetValue(col, Row("Phone", "old"), null)!;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.StartsWith("+1"), $"Phone '{result}' does not start with +1");
        }

        [TestMethod]
        public void GetValue_StringFormat_ReturnsFormattedString()
        {
            var col = new ColumnConfig { Name = "Code", Type = DataType.StringFormat, StringFormatPattern = "###-???-###" };
            var result = (string)Provider().GetValue(col, Row("Code", "old"), null)!;
            Assert.IsNotNull(result);
            Assert.AreEqual(11, result.Length, $"Expected length 11 for pattern '###-???-###', got '{result}'");
        }

        [TestMethod]
        public void GetValue_Bogus_ReturnsParsedValue()
        {
            var col = new ColumnConfig { Name = "Email", Type = DataType.Bogus, StringFormatPattern = "{{internet.email}}" };
            var result = (string)Provider().GetValue(col, Row("Email", "old@old.com"), null)!;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("@"), $"Expected email format, got '{result}'");
        }

        [TestMethod]
        public void GetValue_None_WithUseValue_ReturnsExactString()
        {
            var col = new ColumnConfig { Name = "Status", Type = DataType.None, UseValue = "active" };
            var result = Provider().GetValue(col, Row("Status", "inactive"), null);
            Assert.AreEqual("active", result);
        }

        // ── null / empty handling ────────────────────────────────────────────

        [TestMethod]
        public void GetValue_RetainNullValues_True_WhenNull_ReturnsNull()
        {
            var col = new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, RetainNullValues = true };
            var result = Provider().GetValue(col, Row("FirstName", null!), null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetValue_RetainNullValues_False_WhenNull_GeneratesValue()
        {
            var col = new ColumnConfig { Name = "FirstName", Type = DataType.FirstName, RetainNullValues = false };
            var result = Provider().GetValue(col, Row("FirstName", null!), null);
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ToString()));
        }

        [TestMethod]
        public void GetValue_RetainEmptyString_True_WhenEmpty_PreservesEmpty()
        {
            var col = new ColumnConfig { Name = "LastName", Type = DataType.LastName, RetainEmptyStringValues = true };
            var result = Provider().GetValue(col, Row("LastName", ""), null);
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void GetValue_RetainEmptyString_True_WhenWhitespace_PreservesWhitespace()
        {
            var col = new ColumnConfig { Name = "LastName", Type = DataType.LastName, RetainEmptyStringValues = true };
            var result = Provider().GetValue(col, Row("LastName", "   "), null);
            Assert.AreEqual("   ", result);
        }

        [TestMethod]
        public void GetValue_RetainEmptyString_False_WhenEmpty_GeneratesValue()
        {
            var col = new ColumnConfig { Name = "LastName", Type = DataType.LastName, RetainEmptyStringValues = false };
            var result = Provider().GetValue(col, Row("LastName", ""), null);
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ToString()));
        }

        // ── UseValue overrides everything ────────────────────────────────────

        [TestMethod]
        public void GetValue_UseValue_OverridesNullRetain()
        {
            var col = new ColumnConfig { Name = "Password", Type = DataType.None, UseValue = "fixed", RetainNullValues = true };
            var result = Provider().GetValue(col, Row("Password", null!), null);
            Assert.AreEqual("fixed", result, "UseValue should take priority over RetainNullValues");
        }

        // ── UseList ──────────────────────────────────────────────────────────

        [TestMethod]
        public void GetValue_UseList_ReturnsValueFromList()
        {
            var allowed = new object[] { "red", "green", "blue" };
            var col = new ColumnConfig { Name = "Colour", Type = DataType.None, UseList = allowed };
            var provider = Provider();

            for (int i = 0; i < 20; i++)
            {
                var result = provider.GetValue(col, Row("Colour", "old"), null);
                CollectionAssert.Contains(allowed, result, $"Unexpected value '{result}' not in UseList");
            }
        }

        // ── Local value mappings ─────────────────────────────────────────────

        [TestMethod]
        public void GetValue_UseLocalValueMappings_SameInputMapsTOSameOutput()
        {
            var col = new ColumnConfig { Name = "LastName", Type = DataType.LastName, UseLocalValueMappings = true };
            var provider = Provider();

            var first  = provider.GetValue(col, Row("LastName", "Smith"), null);
            var second = provider.GetValue(col, Row("LastName", "Smith"), null);
            var third  = provider.GetValue(col, Row("LastName", "Smith"), null);

            Assert.AreEqual(first, second, "Same input should always map to same masked output (local mappings)");
            Assert.AreEqual(first, third);
        }

        [TestMethod]
        public void GetValue_UseLocalValueMappings_DifferentInputsMapToDifferentOutputs()
        {
            var col = new ColumnConfig { Name = "LastName", Type = DataType.LastName, UseLocalValueMappings = true };
            var provider = Provider();

            var resultForSmith = provider.GetValue(col, Row("LastName", "Smith"), null);
            var resultForJones = provider.GetValue(col, Row("LastName", "Jones"), null);

            // Each distinct original value gets its own masked replacement
            Assert.AreNotEqual(resultForSmith, resultForJones,
                "Different original values should not map to identical masked values (probabilistic, but overwhelmingly likely)");
        }

        // ── Global value mappings ────────────────────────────────────────────

        [TestMethod]
        public void GetValue_UseGlobalValueMappings_SameInputMapsToSameOutputAcrossInstances()
        {
            var provider = Provider();

            var colA = new ColumnConfig { Name = "LastName", Type = DataType.LastName, UseGlobalValueMappings = true };
            var colB = new ColumnConfig { Name = "LastName", Type = DataType.LastName, UseGlobalValueMappings = true };

            var first  = provider.GetValue(colA, Row("LastName", "Smith"), null);
            var second = provider.GetValue(colB, Row("LastName", "Smith"), null);

            Assert.AreEqual(first, second, "Global mappings: same column + same input value should always produce the same masked value");
        }

        // ── Gender-aware name generation ─────────────────────────────────────

        [TestMethod]
        public void GetValue_FirstName_WithMaleGender_ReturnsNonEmptyString()
        {
            var result = Provider().GetValue(
                Col("FirstName", DataType.FirstName),
                Row("FirstName", "Original"),
                Bogus.DataSets.Name.Gender.Male);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)result));
        }

        [TestMethod]
        public void GetValue_FirstName_WithFemaleGender_ReturnsNonEmptyString()
        {
            var result = Provider().GetValue(
                Col("FirstName", DataType.FirstName),
                Row("FirstName", "Original"),
                Bogus.DataSets.Name.Gender.Female);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)result));
        }

        // ── Locale ───────────────────────────────────────────────────────────

        [TestMethod]
        public void GetValue_NonEnglishLocale_ReturnsNonEmptyString()
        {
            var provider = new BogusDataProvider(new DataGenerationConfig { Locale = "de" });
            var result = provider.GetValue(Col("FirstName", DataType.FirstName), Row("FirstName", "Klaus"), null);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)result));
        }

        [TestMethod]
        public void GetValue_NullLocale_DefaultsToEnglish()
        {
            var provider = new BogusDataProvider(new DataGenerationConfig { Locale = null });
            var result = provider.GetValue(Col("FirstName", DataType.FirstName), Row("FirstName", "Steve"), null);
            Assert.IsNotNull(result);
        }

        // ── DataType.None edge cases ──────────────────────────────────────────

        [TestMethod]
        public void GetValue_NoneType_NoUseValueOrUseList_NonNullExistingValue_ThrowsArgumentOutOfRangeException()
        {
            // DataType.None is not handled in the private GetValue switch, so if execution
            // reaches it (no UseValue, no UseList, existing value is non-null) it throws.
            var col = new ColumnConfig { Name = "Status", Type = DataType.None };
            try
            {
                Provider().GetValue(col, Row("Status", "active"), null);
                Assert.Fail("Expected ArgumentOutOfRangeException for DataType.None with no UseValue/UseList");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void GetValue_NoneType_NullExistingValue_RetainNullTrue_ReturnsNull()
        {
            // RetainNullValues short-circuits before the unhandled None switch branch
            var col = new ColumnConfig { Name = "Status", Type = DataType.None, RetainNullValues = true };
            var result = Provider().GetValue(col, Row("Status", null!), null);
            Assert.IsNull(result);
        }

        // ── Pre-populated ValueMappings with UseLocalValueMappings ────────────

        [TestMethod]
        public void GetValue_PrePopulatedValueMappings_KnownKey_ReturnsMappedValue()
        {
            var col = new ColumnConfig
            {
                Name                 = "LastName",
                Type                 = DataType.LastName,
                UseLocalValueMappings = true,
                ValueMappings        = new Dictionary<object, object> { { "Smith", "Jones" } }
            };

            // "Smith" is pre-seeded → should always return "Jones" without generating a new value
            var result = Provider().GetValue(col, Row("LastName", "Smith"), null);
            Assert.AreEqual("Jones", result,
                "Pre-populated ValueMappings should be returned directly when the key is found");
        }

        [TestMethod]
        public void GetValue_PrePopulatedValueMappings_UnknownKey_GeneratesAndStoresNewMapping()
        {
            var col = new ColumnConfig
            {
                Name                 = "LastName",
                Type                 = DataType.LastName,
                UseLocalValueMappings = true,
                ValueMappings        = new Dictionary<object, object> { { "Smith", "Jones" } }
            };
            var provider = Provider();

            // "Brown" is not pre-seeded → a new value is generated and stored
            var firstCall  = provider.GetValue(col, Row("LastName", "Brown"), null);
            var secondCall = provider.GetValue(col, Row("LastName", "Brown"), null);

            Assert.IsNotNull(firstCall);
            Assert.AreEqual(firstCall, secondCall,
                "After the first call, the same input should always produce the same masked output");
        }

        [TestMethod]
        public void GetValue_PrePopulatedValueMappings_PreSeededKeyNotAffectedByOtherInputs()
        {
            var col = new ColumnConfig
            {
                Name                 = "LastName",
                Type                 = DataType.LastName,
                UseLocalValueMappings = true,
                ValueMappings        = new Dictionary<object, object> { { "Smith", "Jones" } }
            };
            var provider = Provider();

            // Generate mapping for an unknown value first
            provider.GetValue(col, Row("LastName", "Brown"), null);

            // Pre-seeded "Smith" → "Jones" should still hold
            var result = provider.GetValue(col, Row("LastName", "Smith"), null);
            Assert.AreEqual("Jones", result,
                "Pre-seeded mapping should not be overwritten by auto-generated mappings for other keys");
        }

        // ── Exception throw paths ────────────────────────────────────────────

        [TestMethod]
        public void GetValue_SqlType_ThrowsArgumentOutOfRangeException()
        {
            // DataType.Sql is not handled in the private GetValue switch
            var col = new ColumnConfig { Name = "X", Type = DataType.Sql };
            try
            {
                Provider().GetValue(col, Row("X", "existing"), null);
                Assert.Fail("Expected ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void GetValue_UseValue_SqlType_ConvertValue_ThrowsArgumentOutOfRangeException()
        {
            // UseValue triggers ConvertValue, which does not handle DataType.Sql
            var col = new ColumnConfig { Name = "X", Type = DataType.Sql, UseValue = "something" };
            try
            {
                Provider().GetValue(col, Row("X", "existing"), null);
                Assert.Fail("Expected ArgumentOutOfRangeException from ConvertValue");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void GetValue_UseValue_ComputedType_ConvertValue_ThrowsArgumentOutOfRangeException()
        {
            // UseValue triggers ConvertValue, which does not handle DataType.Computed
            var col = new ColumnConfig { Name = "X", Type = DataType.Computed, UseValue = "val" };
            try
            {
                Provider().GetValue(col, Row("X", "existing"), null);
                Assert.Fail("Expected ArgumentOutOfRangeException from ConvertValue");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void GetValue_UseGlobalValueMappings_TwoDifferentInputs_BothRetainedSeparately()
        {
            var col = new ColumnConfig { Name = "LastName", Type = DataType.LastName, UseGlobalValueMappings = true };
            var provider = Provider();

            // First call creates _globalValueMappings["LastName"] dictionary
            var mappedSmith = provider.GetValue(col, Row("LastName", "Smith"), null);
            // Second call with different input goes into the ContainsKey=true branch
            var mappedJones = provider.GetValue(col, Row("LastName", "Jones"), null);

            // Subsequent calls should return the same mapped values
            Assert.AreEqual(mappedSmith, provider.GetValue(col, Row("LastName", "Smith"), null));
            Assert.AreEqual(mappedJones, provider.GetValue(col, Row("LastName", "Jones"), null));
        }

        // ── ConvertValue for DateOfBirth with UseValue ───────────────────────

        [TestMethod]
        public void GetValue_UseValue_DateOfBirth_ReturnsDateTime()
        {
            var col = new ColumnConfig
            {
                Name = "DOB",
                Type = DataType.DateOfBirth,
                UseValue = "2000-01-15"
            };
            var result = Provider().GetValue(col, Row("DOB", DateTime.Now), null);
            Assert.IsInstanceOfType(result, typeof(DateTime));
            Assert.AreEqual(new DateTime(2000, 1, 15), (DateTime)result);
        }
    }
}
