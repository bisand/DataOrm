using System;

namespace DataOrm.DataAccess.Common.Attributes
{
    public class NavigationPropertyAttribute : Attribute
    {
        /// <summary>
        ///     Foreign key to identify records in the referencing table
        /// </summary>
        public string ForeignKey { get; set; }

        /// <summary>
        ///     Local key to identify match with foreign key.
        /// </summary>
        public string LocalKey { get; set; }

        /// <summary>
        ///     Entity name. Can be used if the property name differs from the database naming convention.
        ///     If this value is not set, the system will search for a table that matches the property name.
        ///     For enumerable properties, the property should be named in plural. For single object properties
        ///     the property should be named in singular. The system will automatically pluralize the table name
        ///     when querying the database.
        /// </summary>
        public string Entity { get; set; }
    }
}