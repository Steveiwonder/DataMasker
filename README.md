# DataMasker
A free data masking and/or anonymizer library for Sql Server

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
          "type": "Bogus",
          "retainNullValues": false,
          "stringFormatPattern": "{{address.fullAddress}}"
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

All of the [objects/datatypes](https://github.com/bchavez/Bogus#bogus-api-support) from Bogus are supported, you can use the type "Bogus" in combination with "stringFormatPattern" to acheive any valueMappings

```json
{
  "name": "Address",
  "type": "Bogus",
  "stringFormatPattern": "{{address.fullAddress}}"
}
```

You can combine multiple objects to generate complex data

```json
{
  "name": "Name",
  "type": "Bogus",
  "stringFormatPattern": "{{name.prefix}} {{name.firstName}} {{name.lastName}}"
}
```
## Data Sources
There are only two `DataSources` available at the moment
* `InMemoryFake` - is there only for the examples
* `SqlServer` - can pull and push data to SQL Server

There is some additional configuration required when using  `SqlServer`, on the `dataSource` object a dynamic `config` property is available, you'll need to supply the name, server, userName & password for the connection
```json
{
  "dataSource":{
    "config":{
      "name": "xxx",
      "server": "xxx",
      "userName": "xxx",
      "password": "xxx"
    }
  }
}
```


## Gender
To ensure the new data is more accurate and believable you may want to take gender into consideration when generating certain data types such as names. This can be achieved with a small about of additional config. If no gender is specified then non gender specific names are generated.

You must define the gender column and then tell your target column to use this when generating data.

Here we are saying, use the column "Gender" when generating data for "FirstName".We then include the "Gender" column but tell it to be ignored by the `IDataMasker`, it is purley there as a dependency to "FirstName"

```json
"columns": [
  {
    "name": "FirstName",
    "type": "FirstName",
    "useGenderColumn": "Gender"
  },
  {
    "name": "Gender",
    "ignore": true,
    "type": "None"
  },  
]
```

## Locale
By default the locale is "en", the locale.

The locale is used by [Bogus](https://github.com/bchavez/Bogus#locales) to generate data, the locale can be changedby setting a property on the dataGeneration object
```json
{
  "dataGeneration": {
      "locale": "en"
    }  
}
```

Check out the Bogus page for a [list of supported locales](https://github.com/bchavez/Bogus#locales)
