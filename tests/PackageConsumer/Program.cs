using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using DataMasker;
using DataMasker.Interfaces;
using DataMasker.Models;
using Microsoft.Data.SqlClient;

internal static class Program
{
    private static void Main()
    {
        // Verify that NuGet chose the intended packaged asset, rather than a project reference.
        string target = typeof(DataMasker.DataMasker).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
#if NETFRAMEWORK
        Require(target == ".NETStandard,Version=v2.0", "Expected .NET Standard package asset.");
#else
        Require(target == ".NETCoreApp,Version=v10.0", "Expected modern .NET package asset.");
#endif

        string configPath = Path.Combine(Path.GetTempPath(), "datamasker-consumer-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(configPath, @"{
              ""dataSource"": { ""type"": ""InMemoryFake"" },
              ""tables"": [{ ""name"": ""Users"", ""primaryKeyColumn"": ""UserId"",
                ""columns"": [{ ""name"": ""FirstName"", ""type"": ""FirstName"" },
                                { ""name"": ""Password"", ""type"": ""None"", ""useValue"": ""MASKED"" }] }]
            }");
            Config config = Config.Load(configPath);
            TableConfig table = config.Tables.Single();
            IDataSource source = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);
            var masker = new DataMasker.DataMasker(new IDataProvider[]
            {
                new BogusDataProvider(new DataGenerationConfig { Locale = "en" })
            });
            var rows = source.GetData(table).Select(row => masker.Mask(row, table)).ToList();
            Require(rows.Count == 3, "Expected all seeded rows.");
            Require(rows.All(row => Equals(row["Password"], "MASKED")), "Constant masking failed.");
            Require(rows.All(row => !string.IsNullOrWhiteSpace((string)row["FirstName"])), "Bogus generation failed.");
            int updated = 0;
            source.UpdateRows(rows, rows.Count, table, count => updated = count);
            Require(updated == 3, "Update callback failed.");
            Require(source.GetData(table).All(row => Equals(row["Password"], "MASKED")), "Masked values were not saved.");

            // Loading and constructing SqlClient catches unsupported/stub runtime assembly selection.
            using (var connection = new SqlConnection("Server=localhost;Database=master;Integrated Security=true;Encrypt=true"))
            {
                Require(connection.Database == "master", "SqlClient runtime asset failed to initialize.");
            }
            Console.WriteLine("Package consumer passed: " + target + " on " + Environment.Version);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
