DataOrm
=======

Lightweight ORM

This is a lightweight Object Relational Mapper for querying databases with SQL and returning a list of strongly typed objects. You can also insert, update and retrive "navigational properties" within the objects.

Database support:
* SQL Server
* More will come..
 
 
###Example:

    var connectionString = "Data Source=localhost;Initial Catalog=Test;Integrated Security=True;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False";
    using (var session = DataOrmServer.CreateSession(SessionType.SqlServer, connectionString))
    {
        var activities = new List<Activity>();
        var result = session.Query<Activity>("SELECT * FROM Activities");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }
