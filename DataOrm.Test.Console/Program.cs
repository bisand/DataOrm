using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using DataOrm.DataAccess.Communication;
using DataOrm.DataAccess.Communication.Implementations;
using DataOrm.Tests.Integration.Models;

namespace DataOrm.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            int rows = 10000;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            TestMySql(rows);
            Console.WriteLine("Inserted " + rows + " records in " + stopwatch.ElapsedMilliseconds + " ms.");
            stopwatch.Stop();
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());
        }

        static void TestMySql(int rows)
        {
            var connectionString = "user=root;password=SuperStrongPassword!;server=localhost;database=test;Character Set=utf8";
            using (var session = DataOrmServer.CreateSession(SessionType.MySql, connectionString))
            {
                var list = new List<EntityNormalizedValue>();
                for (int i = 0; i < rows; i++)
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
