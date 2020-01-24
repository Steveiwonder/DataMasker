![https://ci.appveyor.com/api/projects/status/g5smobkt3j6gom43/branch/master?svg=true](https://ci.appveyor.com/api/projects/status/g5smobkt3j6gom43/branch/master?svg=true)

# DataMasker
A free data masking and/or anonymizer library for Sql Server written in .NET

If you've ever needed to pull down databases from a live environment to stage or even dev you'll need to think about masking any personal information. There are options out there paid and free, however the free ones I've found do not provide genuine data and the paid options are too pricey when it's only a few tables.

Data generation is provided by [https://github.com/bchavez/Bogus](https://github.com/bchavez/Bogus)
#### Bogus
>Hello. I'm your host Brian Chavez (twitter). Bogus is a simple and sane fake data generator for .NET languages like C#, F# and VB.NET. Bogus is fundamentally a C# port of faker.js and inspired by FluentValidation's syntax sugar.

>Bogus will help you load databases, UI and apps with fake data for your testing needs. If you like Bogus star ⭐️ the repository and show your friends! 😄

## Nuget
DataMasker is available as a library over on [nuget.org](https://www.nuget.org/packages/DataMasker/) should you need to customize any part of the process. Alternatively just ask for a new feature and I'll see what I can do.

>install-package DataMasker

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
      "schema": "dbo",
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
          "retainEmptyStringValues": true,
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
          "type": "DateOfBirth",
          "min" :"1901-01-01",
          "max": "2000-0101"
        },
        {
          "name": "EmailAddress",
          "type": "Bogus",
          "unique": true,
          "stringFormatPattern": "{{internet.email}}"
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

## Column Configuration

| Property Name | Values |
| ------------- | ------ |
| type | None, Bogus, FirstName, lastName, DateOfBirth, Rant, StringFormat, FullAddress, PhoneNumber |
| name | Database column name |
| schema | Name of the schema in which the tables lives, defaults to dbo
| valueMappings | Object with value mappings, *e.g map "James" to "John"*
| useGenderColumn | Name of the database column to use as for the gender |
| ignore | true/false
| min | Minimum value to use for the given data type |
| max | Maxiumum value to use for the given data type |
| stringFormatPattern | From [Bogus](https://github.com/bchavez/Bogus#replace), numbers #, letters ?, or * random number or letter |
| useValue | A hardcoded value to use for every row |
| retainNullValues | true/false |
| retainEmptyStringValues | true/false - when true if the existing value is null or empty (whitespace) then it will use the original value |
| useLocalValueMappings | true/false |
| useGlobalValueMappings | true/false |
| unique | true/false - when true it will attempt to generate a unique value for this column|

##### None
To use None you must specify either `valueMappings` or `useValue`, no data will be generated for this type. If you specify only `valueMappings` and the target value is not found, an error will be thrown.
```json
{
  "name": "Title",
  "type": "None",
  "valueMappings": {    
    "Mr": "Master"
  },
  "ignore":"true/false",
  "useValue": "Miss",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false"
}
```

##### Bogus
Bogus is a type that when specified requires the `stringFormatPattern` option, which is passed directly to the Bogus API, see [here](https://github.com/bchavez/Bogus#bogus-api-support) for available options

```json
{
  "name": "PhoneNumber",
  "type": "Bogus",
  "valueMappings": {    
    "+555-555-555": "+444-555-555-55"
  },
  "ignore":"true/false",
  "useValue": "+50559-5-5-555",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
  "stringFormatPattern": "{{phonenumbers.phonenumber}}"
}
```

##### FirstName
```json
{
  "name": "FirstName",
  "type": "FirstName",
  "valueMappings": {    
    "James": "Bob"
  },
  "ignore":"true/false",
  "useValue": "Steve",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
  "useGenderColumn": "Gender"
}
```

##### LastName

```json
{
  "name": "Surname",
  "type": "LastName",
  "valueMappings": {    
    "Smith": "Jojnes"
  },
  "ignore":"true/false",
  "useValue": "Timms",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false"
}
```
##### DateOfBirth
- `min` will default to 1901-01-01
- `max` will default to current date
- date format is `fullyear-month-day`



```json
{
  "name": "DOB",
  "type": "DateOfBirth",
  "valueMappings": {    
    "1990-01-02": "1990-02-02"
  },
  "ignore":"true/false",
  "useValue": "1940-02-02",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
  "min": "1901-12-25",
  "max": "2000-11-20"
}
```

##### Rant
- `max` will default to 25

```json
{
  "name": "Comments",
  "type": "Rant",
  "valueMappings": {    
    "A comment": "Becomes this"
  },
  "ignore":"true/false",
  "useValue": "A really important comment",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
  "max": 15
}
```

##### Lorem
- `min` will default to 5
- `max` will default to 30

```json
{
  "name": "Comments",
  "type": "Lorem",
  "valueMappings": {    
    "A comment": "Becomes this"
  },
  "ignore":"true/false",
  "useValue": "A really important comment",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
  "min": 5,
  "max": 15
}
```
##### StringFormat

Check out the [Bogus API](https://github.com/bchavez/Bogus#bogus-api-support) for supported values

```json
{
  "name": "Comments",
  "type": "StringFormat",
  "valueMappings": {    
    "A comment": "Becomes this"
  },
  "ignore":"true/false",
  "useValue": "A really important comment",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
  "stringFormatPattern": "#####****?????"
}
```

##### FullAddress

```json
{
  "name": "Address",
  "type": "FullAddress",
  "valueMappings": {    
    "55 Long Name Street, Long Name Village, Long Name Town...": "Becomes this"
  },
  "ignore":"true/false",
  "useValue": "55 Long Name Street, Long Name Village, Long Name Town...",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
}
```

##### PhoneNumber

```json
{
  "name": "PhoneNumber",
  "type": "PhoneNumber",
  "valueMappings": {    
    "+555-555-555": "+444-555-555-55"
  },
  "ignore":"true/false",
  "useValue": "+50559-5-5-555",
  "retainNullValues": "true/false",
  "retainEmptyStringValues": "true/false",
  "useLocalValueMappings": "true/false",
  "useGlobalValueMappings": "true/false",
  "stringFormatPattern": "+1 ########-#-###-#"
}
```

`name` & `type` are required everything else is optional unless specified

Most data can be generated perfectly fine just by using the `Bogus` or `StringFormat` data types.

## Min & max
Only some data types currently use the min/max properties on the column configurations.

* Lorem, Rant & DOB

Example Usage
```csharp
//load our configuration
Config config = Config.Load($"example-configs\\config-example1.json");

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
    var masked = new List<IDictionary<string, object>>();
    foreach (IDictionary<string, object> row in rows)
    {
        //mask each row
        masked.Add(dataMasker.Mask(row, tableConfig));

        //update per row, or see below,
        //dataSource.UpdateRow(row, tableConfig);
    }

    //update all rows, in batches of 100
    dataSource.UpdateRows(masked, 100, tableConfig);
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

There is some additional configuration required when using  `SqlServer`, on the `dataSource` object a dynamic `config` property is available, you'll need to supply the name, server, userName & password for the connection or a connection string.

*N.B. if the "connectionString" value is set name, server, userName & password will be ignored*
```json
{
  "dataSource":{
    "config":{
      "name": "xxx",
      "server": "xxx",
      "userName": "xxx",
      "password": "xxx",
      "connectionString": "Data Source=(localdb)\\mssqllocaldb;Initial Catalog=Clients;Integrated Security=SSPI;"
    }
  }
}
```
### `SqlServer` Data Source
Dry run is supported. A transaction is created, the update statement is executed and then the transaction is rolled back.

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

## DataMasker.Runner (CLI)

The latest CLI build can be found [here](https://ci.appveyor.com/project/Steveiwonder/datamasker/build/artifacts)

This is a CLI interface for the data masking tool. You might want to use this as part of your continuous integration if you backup/restore your live environments back to stage/dev after a release

The options are as follows


>  -c, --config-file         Required. the json configuration to be
>
>  -d, --dry-run             (Default: false) dry run, only supported by some data sources
>
>  -l, --locale              set the locale
>
>  -u, --update-batchsize    batch size to use when upating records
>
>  --print-options           (Default: false) prints the arguments passed into this tool in a json format with executing
>                            anything else
>
>  --no-output               (Default: false) if set, no output to the console will be written
>
>  --help                    Display this help screen.
>
>  --version                 Display version information.
