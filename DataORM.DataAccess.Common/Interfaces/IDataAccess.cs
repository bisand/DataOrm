using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using DataOrm.DataAccess.Common.Threading;

namespace DataOrm.DataAccess.Common.Interfaces
{
    public interface IDataAccess : IDisposable
    {
        string ConnectionString { get; set; }
        int? ConnectionTimeout { get; set; }

        /// <summary>
        ///     Format used when parsing and settings values of type DateTime.
        /// </summary>
        string[] DateTimeFormats { get; set; }

        List<DbParameter> Parameters { get; set; }

        List<T> Query<T>(string query) where T : new();
        IDbCommand CreateCommand(string sql, LoadWithOption option = null, CommandType commandType = CommandType.Text, List<DbParameter> parameters = null);
        void LoadWith<T>(Expression<Func<T, Object>> expression);
        bool InsertData<T>(List<T> dataList, string entityName = null) where T : new();
        bool UpdateData<T>(List<T> dataList, string entityName = null);

        void AddParameter(string name, object value, DbType dbType);
    }
}