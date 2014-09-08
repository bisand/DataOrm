using System;
using DataOrm.DataAccess.Communication;
using DataOrm.Tests.Integration.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataOrm.Tests.Integration
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var sqlServer = new SqlServer("Data Source=localhost;Initial Catalog=VRIntegration;Integrated Security=True;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False");
            var result = sqlServer.Query<JobConfiguration>("SELECT * FROM vw_JobConfigurations");
            Assert.IsNotNull(result);
        }
    }
}
