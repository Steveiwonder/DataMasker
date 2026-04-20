using DataMasker.Models;
using DataMasker.Utils;
using DMUtils = DataMasker.Utils.Utils;

namespace DataMasker.Tests.Utils
{
    [TestClass]
    public class UtilsTests
    {
        // ── TryParseGender ────────────────────────────────────────────────────

        [TestMethod]
        public void TryParseGender_Male_ReturnsMale()
        {
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, DMUtils.TryParseGender("m"));
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, DMUtils.TryParseGender("male"));
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, DMUtils.TryParseGender("M"));
        }

        [TestMethod]
        public void TryParseGender_Female_ReturnsFemale()
        {
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, DMUtils.TryParseGender("f"));
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, DMUtils.TryParseGender("female"));
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, DMUtils.TryParseGender("F"));
        }

        [TestMethod]
        public void TryParseGender_Null_ReturnsNull()
        {
            Assert.IsNull(DMUtils.TryParseGender(null));
        }

        [TestMethod]
        public void TryParseGender_Unknown_ReturnsNull()
        {
            Assert.IsNull(DMUtils.TryParseGender("unknown"));
        }

        // ── MakeParamNameSafe ─────────────────────────────────────────────────

        [TestMethod]
        public void MakeParamNameSafe_ReplacesSpacesWithUnderscores()
        {
            Assert.AreEqual("First_Name", DMUtils.MakeParamNameSafe("First Name"));
        }

        [TestMethod]
        public void MakeParamNameSafe_ReplacesHyphensWithUnderscores()
        {
            Assert.AreEqual("date_of_birth", DMUtils.MakeParamNameSafe("date-of-birth"));
        }

        [TestMethod]
        public void MakeParamNameSafe_NoSpecialChars_Unchanged()
        {
            Assert.AreEqual("FirstName", DMUtils.MakeParamNameSafe("FirstName"));
        }

        // ── MakeColumnNameSafe ────────────────────────────────────────────────

        [TestMethod]
        public void MakeColumnNameSafe_AddsSquareBrackets()
        {
            Assert.AreEqual("[UserId]", DMUtils.MakeColumnNameSafe("UserId"));
        }

        [TestMethod]
        public void MakeColumnNameSafe_AlreadyBracketed_NoChange()
        {
            Assert.AreEqual("[UserId]", DMUtils.MakeColumnNameSafe("[UserId]"));
        }

        // ── MakeParamNamesSafe ────────────────────────────────────────────────

        [TestMethod]
        public void MakeParamNamesSafe_ConvertsAllKeys()
        {
            var input = new Dictionary<string, object>
            {
                ["First Name"]    = "Steve",
                ["date-of-birth"] = "1990-01-01",
                ["NormalKey"]     = 42
            };

            var result = DMUtils.MakeParamNamesSafe(input);

            Assert.IsTrue(result.ContainsKey("First_Name"),    "Expected 'First_Name'");
            Assert.IsTrue(result.ContainsKey("date_of_birth"), "Expected 'date_of_birth'");
            Assert.IsTrue(result.ContainsKey("NormalKey"),     "Expected 'NormalKey' unchanged");
        }

        [TestMethod]
        public void MakeParamNamesSafe_PreservesValues()
        {
            var input = new Dictionary<string, object> { ["First Name"] = "Steve" };
            var result = DMUtils.MakeParamNamesSafe(input);
            Assert.AreEqual("Steve", result["First_Name"]);
        }

        // ── GetConnectionString ───────────────────────────────────────────────

        [TestMethod]
        public void GetConnectionString_WithConnectionString_ReturnsItDirectly()
        {
            var config = new DataSourceConfig
            {
                Config = Newtonsoft.Json.Linq.JObject.Parse(
                    @"{ ""connectionString"": ""Server=myserver;Database=mydb;Integrated Security=True;"" }")
            };

            string result = config.GetConnectionString();

            Assert.AreEqual("Server=myserver;Database=mydb;Integrated Security=True;", result);
        }

        [TestMethod]
        public void GetConnectionString_WithoutConnectionString_BuildsFromComponents()
        {
            var config = new DataSourceConfig
            {
                Config = Newtonsoft.Json.Linq.JObject.Parse(
                    @"{ ""userName"": ""sa"", ""password"": ""secret"", ""server"": ""localhost"", ""name"": ""MyDb"" }")
            };

            string result = config.GetConnectionString();

            Assert.IsTrue(result.Contains("User ID=sa"),         $"Expected 'User ID=sa' in: {result}");
            Assert.IsTrue(result.Contains("Password=secret"),    $"Expected 'Password=secret' in: {result}");
            Assert.IsTrue(result.Contains("Data Source=localhost"), $"Expected 'Data Source=localhost' in: {result}");
            Assert.IsTrue(result.Contains("Initial Catalog=MyDb"),  $"Expected 'Initial Catalog=MyDb' in: {result}");
        }

        [TestMethod]
        public void GetConnectionString_EmptyConnectionString_FallsBackToComponents()
        {
            var config = new DataSourceConfig
            {
                Config = Newtonsoft.Json.Linq.JObject.Parse(
                    @"{ ""connectionString"": ""   "", ""userName"": ""sa"", ""password"": ""p"", ""server"": ""srv"", ""name"": ""db"" }")
            };

            // whitespace-only connectionString should fall back to building from parts
            string result = config.GetConnectionString();

            Assert.IsTrue(result.Contains("User ID=sa"), $"Whitespace connectionString should fall back to component build, got: {result}");
        }
    }
}
