# DataMasker
A free data masking library.

If you've ever needed to pull down databases from a live environment to stage or even dev you'll need to think about masking any personal information. There are options out there paid and free, however the free ones I've found do not provide genuine data and the paid options are too pricey when it's only a few tables.

Data generation is provided by [https://github.com/bchavez/Bogus](https://github.com/bchavez/Bogus)
#### Bogus
>Hello. I'm your host Brian Chavez (twitter). Bogus is a simple and sane fake data generator for .NET languages like C#, F# and VB.NET. Bogus is fundamentally a C# port of faker.js and inspired by FluentValidation's syntax sugar.

>Bogus will help you load databases, UI and apps with fake data for your testing needs. If you like Bogus star ⭐️ the repository and show your friends! 😄

## Configuration
All configuration is done via a .json file, the schema can be found here: [json schema](https://github.com/Steveiwonder/DataMasker/blob/master/src/DataMasker.Config.schema.json)

Example Config
```json
{
  "dataSource": {
    "type": "SqlServer",
    "config": {
       "name": "databasename",
       "userName": "databaseUserName",
       "password": "databasePassword",
       "server": "databaseServer"
    }   
  },
  "tables": [    
    {
      "name": "Users",
      "primaryKeyColumn": "UserId",
      "columns": [
        {
          "name": "FirstName",
          "type": "FirstName",
          "useGenderColumn": "Gender"
        },
        {
          "name": "LastName",
          "type": "LastName",
          "useGlobalValueMappings": true,
          "valueMappings": {
            "oldValue": "Jones",
            "newValue": "Smith"
          }
        },
        {
          "name": "Password",
          "type": "None",
          "retainNullValues": false,
          "useValue": "PasswordForDevEnvironment"
        },
        {
          "name": "DOB",
          "type": "DateOfBirth"
        },
        {
          "name": "Gender",
          "ignore": true,
          "type": "None"
        },
        {
          "name": "Address",
          "type": "FullAddress",
          "retainNullValues": false
        },
        {
          "name": "ContactNumber",
          "type": "PhoneNumber",
          "retainNullValues": false,
          "stringFormatPattern": "+447#########"
        }
      ]
    }
  ]
}
```

Example Usage
```csharp
//load our configuration
Config config = Config.LoadConfig($"example-configs\\config-example1.json")

//create a data masker
IDataMasker dataMasker = new DataMasker(new DataGenerator(config.DataGeneration));

//grab our dataSource from the config, note: you could just ignore the config.DataSource.Type
//and initialize your own instance
IDataSource dataSource = DataSourceProvider.Provide(config.DataSource.Type, config.DataSource);

//Enumerable all our tables and being masking the data
foreach (TableConfig tableConfig in config.Tables)
{
    //load the data, this needs optimizing for large tables
    IEnumerable<IDictionary<string, object>> rows = dataSource.GetData(tableConfig);
    foreach (IDictionary<string, object> row in rows)
    {
        //mask each row
        dataMasker.Mask(row, tableConfig);

        //update per row, or see below,
        //dataSource.UpdateRow(row, tableConfig);
    }

    //update all rows, in batches of 100
    dataSource.UpdateRows(rows, tableConfig, 100);
}

```
