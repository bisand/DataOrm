using System;
using System.Collections.Generic;
using System.Data;

namespace DataOrm.DataAccess.Common.Interfaces
{
    public interface IAsyncDataAccess : IDataAccess
    {
        IAsyncResult BeginQuery<T>(string query, AsyncCallback callback, object state, CommandType commandType = CommandType.Text, CommandBehavior behavior = CommandBehavior.Default)
            where T : new();

        List<T> EndQuery<T>(IAsyncResult asyncResult) where T : new();
    }
}