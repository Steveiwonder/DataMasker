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
            Example1();
        }

        private static Config LoadConfig(
            int example)
        {
            return Config.Load($"example-configs\\config-example{example}.json");
        }

        public static void Example1()
        {
            Config config = LoadConfig(1);
            IDataMasker dataMasker = new DataMasker(new DataGenerator(config.DataGeneration));
            IDataSource dataSource = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);

            foreach (TableConfig tableConfig in config.Tables)
            {
                IEnumerable<IDictionary<string, object>> rows = dataSource.GetData(tableConfig);
                foreach (IDictionary<string, object> row in rows)
                {
                    dataMasker.Mask(row, tableConfig);

                    //update per row
                    //dataSource.UpdateRow(row, tableConfig);
                }

                //update all rows
                dataSource.UpdateRows(rows, tableConfig);
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
