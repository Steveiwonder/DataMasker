using DataMasker.Models;

namespace DataMasker.Tests.Models
{
    [TestClass]
    public class SqlValueConfigTests
    {
        [TestMethod]
        public void Properties_CanBeSetAndRead()
        {
            var config = new SqlValueConfig
            {
                Query = "SELECT TOP 1 name FROM lookup",
                ValueHandling = NotFoundValueHandling.KeepValue
            };
            Assert.AreEqual("SELECT TOP 1 name FROM lookup", config.Query);
            Assert.AreEqual(NotFoundValueHandling.KeepValue, config.ValueHandling);
        }

        [TestMethod]
        public void ValueHandling_DefaultsToKeepValue()
        {
            var config = new SqlValueConfig();
            Assert.AreEqual(NotFoundValueHandling.KeepValue, config.ValueHandling);
        }

        [TestMethod]
        public void ValueHandling_Null_CanBeSet()
        {
            var config = new SqlValueConfig { ValueHandling = NotFoundValueHandling.Null };
            Assert.AreEqual(NotFoundValueHandling.Null, config.ValueHandling);
        }

        [TestMethod]
        public void Query_DefaultsToNull()
        {
            var config = new SqlValueConfig();
            Assert.IsNull(config.Query);
        }
    }

    [TestClass]
    public class DataGenerationConfigTests
    {
        [TestMethod]
        public void Default_IsNotNull()
        {
            Assert.IsNotNull(DataGenerationConfig.Default);
        }

        [TestMethod]
        public void Default_LocaleIsNull()
        {
            Assert.IsNull(DataGenerationConfig.Default.Locale);
        }

        [TestMethod]
        public void Default_IsSameInstance()
        {
            Assert.AreSame(DataGenerationConfig.Default, DataGenerationConfig.Default);
        }

        [TestMethod]
        public void Locale_CanBeSetAndRead()
        {
            var config = new DataGenerationConfig { Locale = "fr" };
            Assert.AreEqual("fr", config.Locale);
        }
    }

    [TestClass]
    public class DataSourceConfigTests
    {
        [TestMethod]
        public void Type_CanBeSetAndRead()
        {
            var config = new DataSourceConfig { Type = DataSourceType.SqlServer };
            Assert.AreEqual(DataSourceType.SqlServer, config.Type);
        }

        [TestMethod]
        public void Type_InMemoryFake_CanBeSet()
        {
            var config = new DataSourceConfig { Type = DataSourceType.InMemoryFake };
            Assert.AreEqual(DataSourceType.InMemoryFake, config.Type);
        }

        [TestMethod]
        public void DryRun_DefaultsFalse()
        {
            var config = new DataSourceConfig();
            Assert.IsFalse(config.DryRun);
        }

        [TestMethod]
        public void DryRun_CanBeSetTrue()
        {
            var config = new DataSourceConfig { DryRun = true };
            Assert.IsTrue(config.DryRun);
        }

        [TestMethod]
        public void UpdateBatchSize_DefaultsNull()
        {
            var config = new DataSourceConfig();
            Assert.IsNull(config.UpdateBatchSize);
        }

        [TestMethod]
        public void UpdateBatchSize_CanBeSet()
        {
            var config = new DataSourceConfig { UpdateBatchSize = 100 };
            Assert.AreEqual(100, config.UpdateBatchSize);
        }

        [TestMethod]
        public void Config_CanBeSetToDynamic()
        {
            var config = new DataSourceConfig
            {
                Config = new { connectionString = "Server=.;Database=test;" }
            };
            Assert.IsNotNull(config.Config);
            Assert.AreEqual("Server=.;Database=test;", (string)config.Config.connectionString);
        }
    }
}
