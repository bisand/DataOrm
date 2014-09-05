using System;

namespace DataOrm.DataAccess.Common.Attributes
{
    public class DatabaseFieldAttribute : Attribute
    {
        public string FieldName { get; set; }
        public bool IsKey { get; set; }
    }
}