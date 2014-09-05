using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using DataOrm.DataAccess.Common.Threading;

namespace DataOrm.DataAccess.Common.Interfaces
{
    public interface IAsyncDataAccess : IDataAccess
    {
        IAsyncResult BeginQuery<T>(string query, AsyncCallback callback, object state, CommandType commandType = CommandType.Text, CommandBehavior behavior = CommandBehavior.Default)
            where T : new();
        List<T> EndQuery<T>(IAsyncResult asyncResult) where T : new();
        IDbCommand CreateCommand(string sql, LoadWithOption option = null, CommandType commandType = CommandType.Text, List<DbParameter> parameters = null);
        void LoadWith<T>(Expression<Func<T, Object>> expression);
        bool InsertData<T>(List<T> dataList, string entityName = null) where T : new();
        bool UpdateData<T>(List<T> dataList, string entityName = null);
    }
}