namespace DataMasker.Tests.Utils
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void TryParseGender_Male_ReturnsMale()
        {
            var result = DataMasker.Utils.Utils.TryParseGender("m");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, result);

            result = DataMasker.Utils.Utils.TryParseGender("male");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, result);

            result = DataMasker.Utils.Utils.TryParseGender("M");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Male, result);
        }

        [TestMethod]
        public void TryParseGender_Female_ReturnsFemale()
        {
            var result = DataMasker.Utils.Utils.TryParseGender("f");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, result);

            result = DataMasker.Utils.Utils.TryParseGender("female");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, result);

            result = DataMasker.Utils.Utils.TryParseGender("F");
            Assert.AreEqual(Bogus.DataSets.Name.Gender.Female, result);
        }

        [TestMethod]
        public void TryParseGender_Null_ReturnsNull()
        {
            var result = DataMasker.Utils.Utils.TryParseGender(null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TryParseGender_Unknown_ReturnsNull()
        {
            var result = DataMasker.Utils.Utils.TryParseGender("unknown");
            Assert.IsNull(result);
        }
    }
}
