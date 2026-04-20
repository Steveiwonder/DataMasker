using DataMasker.DataSources;
using DataMasker.Models;

namespace DataMasker.Tests
{
    [TestClass]
    public class DataSourceProviderTests
    {
        [TestMethod]
        public void Provide_InMemoryFake_ReturnsInMemoryFakeDataSource()
        {
            var source = DataSourceProvider.Provide(DataSourceType.InMemoryFake);
            Assert.IsInstanceOfType(source, typeof(InMemoryFakeDataSource));
        }

        [TestMethod]
        public void Provide_SqlServer_ReturnsSqlDataSource()
        {
            var config = new DataSourceConfig
            {
                Type   = DataSourceType.SqlServer,
                Config = Newtonsoft.Json.Linq.JObject.Parse(@"{ ""connectionString"": ""Server=.;Database=test;"" }")
            };
            var source = DataSourceProvider.Provide(DataSourceType.SqlServer, config);
            Assert.IsInstanceOfType(source, typeof(SqlDataSource));
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
