using System;

namespace DataOrm.DataAccess.Common.Threading
{
    public class LoadWithOption
    {
        public int RecursionLevel;
        public Type DeclaringType { get; set; }
        public Type SourceType { get; set; }
        public Type PropertyType { get; set; }
        public string PropertyName { get; set; }
        public string ForeignKey { get; set; }
        public string LocalKey { get; set; }
        public string EntityName { get; set; }
    }
}