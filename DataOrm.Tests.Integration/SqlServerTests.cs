using System;
using System.Collections.Generic;
using System.Diagnostics;
using DataOrm.DataAccess.Communication;
using DataOrm.DataAccess.Communication.Implementations;
using DataOrm.Tests.Integration.Models;
using Xunit;

namespace DataOrm.Tests.Integration
{
    public class SqlServerTests
    {
        [Fact]
        public void Fact1()
        {
            var sqlServer = new SqlServer("Data Source=localhost;Initial Catalog=VRIntegration;Integrated Security=True;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False");
            var result = sqlServer.Query<JobConfiguration>("SELECT * FROM vw_JobConfigurations");
            Assert.True(result != null);
        }

        [Fact]
        public void PerformaceTest()
        {
            var connectionString = "Data Source=localhost;Initial Catalog=VRIntegration;Integrated Security=True;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False";
            using (var session = DataOrmServer.CreateSession(SessionType.SqlServer, connectionString))
            {
                var activities = new List<DataOrm.Tests.Integration.Models.Activity>();
                var stopwatch = new Stopwatch();
                for (var y = 0; y < 100; y++)
                {
                    stopwatch.Restart();
                    for (var i = 0; i < 500; i++)
                    {
                        var result = session.Query<DataOrm.Tests.Integration.Models.Activity>("SELECT * FROM Activities WHERE ActivityNo = " + i);
                        activities.AddRange(result);
                    }
                    stopwatch.Stop();
                    var elapsed = stopwatch.ElapsedMilliseconds;
                    Console.WriteLine("Elapsed: {0} ms.", elapsed);
                }
                Console.WriteLine("Records: {0}.", activities.Count);
            }
        }

        [Fact]
        public void SimpleExampleTest()
        {
            var connectionString = "Data Source=localhost;Initial Catalog=VRIntegration;Integrated Security=True;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False";
            using (var session = DataOrmServer.CreateSession(SessionType.SqlServer, connectionString))
            {
                var activities = new List<DataOrm.Tests.Integration.Models.Activity>();
                var result = session.Query<DataOrm.Tests.Integration.Models.Activity>("SELECT * FROM Activities");
                Assert.True(result != null);
                Assert.True(result.Count > 0);
            }
        }
    }
}