using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace DataOrm.DataAccess.Common.Interfaces
{
    public interface IDataAccess
    {
        string ConnectionString { get; set; }
        int? ConnectionTimeout { get; set; }

        /// <summary>
        /// Format used when parsing and settings values of type DateTime.
        /// </summary>
        string[] DateTimeFormats { get; set; }
        List<DbParameter> Parameters { get; set; }

        List<T> Query<T>(string query) where T : new();

        void AddParameter(string name, object value, DbType dbType);
    }
}