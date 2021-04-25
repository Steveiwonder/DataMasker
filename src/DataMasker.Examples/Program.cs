using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataMasker.Interfaces;
using DataMasker.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
using Newtonsoft.Json.Serialization;

namespace DataMasker.Examples
{
  internal class Program
  {
    private static void Main(
        string[] args)
    {
      RunExample();
    }

    private static Config LoadConfig(
        int example)
    {
      return Config.Load($"example-configs\\config-example{example}.json");
    }

    public static void RunExample()
    {
      Config config = LoadConfig(3);


      var dataProviders = new List<IDataProvider>();
      dataProviders.Add(new BogusDataProvider(config.DataGeneration));
      dataProviders.Add(new SqlDataProvider(new System.Data.SqlClient.SqlConnection(config.DataSource.Config.connectionString.ToString())));
      //create a data masker
      IDataMasker dataMasker = new DataMasker(dataProviders);

      //grab our dataSource from the config, note: you could just ignore the config.DataSource.Type
      //and initialize your own instance
      IDataSource dataSource = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);

      //enumerate all our tables
      foreach (TableConfig tableConfig in config.Tables)
      {
        //load data
        IEnumerable<IDictionary<string, object>> rows = dataSource.GetData(tableConfig);

        //get row coun
        var rowCount = dataSource.GetCount(tableConfig);


        //here you have two options, you can update all rows in one go, or one at a time.
        #region update all rows
        //update all rows
        var masked = rows.Select(row =>
        {
          //mask the data
          return dataMasker.Mask(row, tableConfig);
        });

        dataSource.UpdateRows(masked, rowCount, tableConfig);
        #endregion
        //OR
        #region update row by row

        foreach (var row in rows)
        {
          //mask the data
          var maskedRow = dataMasker.Mask(row, tableConfig);
          dataSource.UpdateRow(maskedRow, tableConfig);
        }

        #endregion
      }
    }

    private static void GenerateSchema()
    {
      JSchemaGenerator generator = new JSchemaGenerator();

      generator.ContractResolver = new CamelCasePropertyNamesContractResolver();
      JSchema schema = generator.Generate(typeof(Config));
      generator.GenerationProviders.Add(new StringEnumGenerationProvider());
      schema.Title = "DataMasker.Config";
      StringWriter writer = new StringWriter();
      JsonTextWriter jsonTextWriter = new JsonTextWriter(writer);
      schema.WriteTo(jsonTextWriter);
      dynamic parsedJson = JsonConvert.DeserializeObject(writer.ToString());
      dynamic prettyString = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
      StreamWriter fileWriter = new StreamWriter("DataMasker.Config.schema.json");
      fileWriter.WriteLine(schema.Title);
      fileWriter.WriteLine(new string('-', schema.Title.Length));
      fileWriter.WriteLine(prettyString);
      fileWriter.Close();
    }
  }
}
