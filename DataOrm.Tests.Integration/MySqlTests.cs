using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DataOrm.DataAccess.Communication;
using DataOrm.DataAccess.Communication.Implementations;
using DataOrm.Tests.Integration.Models;
using Xunit;

namespace DataOrm.Tests.Integration
{
    public class MySqlTests
    {
        [Fact]
        public void Fact1()
        {
            var mySql = new MySqlServer("user=root;password=SuperStrongPassword!;server=localhost;database=crdb_test;Character Set=utf8");
            var result = mySql.Query<JobConfiguration>("SELECT * FROM vw_JobConfigurations");
            Assert.True(result != null);
        }

        [Fact]
        public void PerformaceTest()
        {
            var connectionString = "user=root;password=SuperStrongPassword!;server=localhost;database=crdb_test;Character Set=utf8";
            using (var session = DataOrmServer.CreateSession(SessionType.MySql, connectionString))
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

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [Fact]
        public void SimpleExampleTest()
        {
            var connectionString = "user=root;password=SuperStrongPassword!;server=localhost;database=crdb_test;Character Set=utf8";
            using (var session = DataOrmServer.CreateSession(SessionType.MySql, connectionString))
            {
                var list = new List<EntityNormalizedValue>();
                for (int i = 0; i < 10000; i++)
                {
                    list.Add(new EntityNormalizedValue
                    {
                        TransactionId = i,
                        BrandId = RandomString(20),
                        BrandName = RandomString(20),
                        EntityName = RandomString(20),
                        EntityNameSpecific = RandomString(20),
                        StreetName = RandomString(20),
                        StreetNumber = RandomString(20),
                        StreetType = RandomString(20),
                        DirectionalPrefix = RandomString(20),
                        ExtensionName = RandomString(20),
                        ExtensionNumber = RandomString(20),
                        PostalCode = RandomString(20),
                        CityName = RandomString(20),
                        CountryCode = RandomString(20),
                        CountryName = RandomString(20),
                        AirportCityCode = RandomString(20),
                        PhoneNumber = RandomString(20),
                        FaxNumber = RandomString(20),
                        NoiseWords = RandomString(20),
                        AttributeValues = RandomString(20),

                        BrandIdFieldSourceId = 1,
                        BrandNameFieldSourceId = 2,
                        StreetNameFieldSourceId = 3,
                        StreetNumberFieldSourceId = 4,
                        StreetTypeFieldSourceId = 5,
                        PostalCodeFieldSourceId = 6,
                        CityNameFieldSourceId = 7,
                        CountryCodeFieldSourceId = 8,
                        AirportCityCodeFieldSourceId = 9,
                        PhoneNumberFieldSourceId = 10,
                        FaxNumberFieldSourceId = 11,
                        QualityLevelId = 12,

                        CreatedTimeUtc = DateTime.UtcNow,
                        OriginalCountryCode = RandomString(2),

                        QualityLevel = 1,

                    });
                }
                session.InsertData(list);
            }
        }
    }
}