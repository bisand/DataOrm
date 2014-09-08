using System;

namespace DataOrm.Tests.Integration.Models
{
    public class JobConfiguration
    {
        public string Category { get; set; }
        public string ConfigType { get; set; }
        public string ConfigName { get; set; }
        public string Configuration { get; set; }
        public int ComponentCategoryNo { get; set; }
        public short JobConfigurationTypeNo { get; set; }
        public bool IsMacro { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}