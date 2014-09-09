using System;

namespace DataOrm.Tests.Integration.Models
{
    public class Activity
    {
        public int ActivityNo { get; set; }
        public int ActivityTypeNo { get; set; }
        public int SourceTypeNo { get; set; }
        public int ActivityStatusNo { get; set; }
        public string SourceGuid { get; set; }
        public string Type { get; set; }
        public string CreatorComponentId { get; set; }
        public string SenderComponentId { get; set; }
        public string ReceiverComponentId { get; set; }
        public decimal SizeInKb { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string SubComponent { get; set; }
        public Guid LinkedId { get; set; }
    }
}