using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace DataOrm.DataAccess.Common.Threading
{
    public class SqlAsyncResult<T> : BasicAsyncResult
    {
        public int NavigationPropertyCount;
        public int WorkCounter;

        public SqlAsyncResult(AsyncCallback callback, object asyncState)
            : base(callback, asyncState)
        {
        }

        public SqlAsyncResult<T> MainAsyncResult { get; set; }
        public IDbCommand Command { get; set; }
        public Func<IDbCommand, CommandBehavior, IDataReader> ExecuteReader { get; set; }
        public Func<string, LoadWithOption, CommandType, List<DbParameter>, IDbCommand> CreateCommand { get; set; }
        public CommandBehavior Behaviour { get; set; }
        public List<T> Result { get; set; }
        public Dictionary<string, object> NavigationProperties { get; set; }
        public Dictionary<LoadWithOption, List<object>> LoadWithOptions { get; set; }
        public string PropertyName { get; set; }
        public List<DbParameter> Parameters { get; set; }
    }
}