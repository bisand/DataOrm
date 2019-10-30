DataOrm
=======

## Lightweight O(R)M for .NET Core

This is a lightweight Object Relational Mapper for querying databases with SQL and returning a list of strongly typed objects. You can also insert, update and retrive "navigational properties".

## Database support
* SQL Server
* MySQL
 
 
## Examples

### SELECT

```csharp
var connectionString = "Data Source=localhost;Initial Catalog=Test;Integrated Security=True;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False";
using (var session = DataOrmServer.CreateSession(SessionType.SqlServer, connectionString))
{
    var result = session.Query<Activity>("SELECT * FROM Activities");
    Assert.IsNotNull(result);
    Assert.IsTrue(result.Count > 0);
}
```

### INSERT

```csharp
var connectionString = "user=root;password=SuperStrongPassword!;server=localhost;database=test;Character Set=utf8";
using (var session = DataOrmServer.CreateSession(SessionType.MySql, connectionString))
{
    var list = new List<EntityNormalizedValue>();
    for (int i = 0; i < 10000; i++)
    {
        list.Add(new EntityNormalizedValue
        {
            TransactionId = i,
            BrandId = "Brand ID " + i,
            BrandName = "Brand name " + i,
        });
    }
    session.InsertData(list);
}
```

## Contributing

Pull requests for new features, bug fixes, and suggestions are welcome!

## License

[MIT](https://github.com/bisand/DataOrm/blob/master/LICENSE)
