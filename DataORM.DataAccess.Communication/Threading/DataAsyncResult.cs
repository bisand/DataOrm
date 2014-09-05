using System;
using System.Collections.Generic;
using DataOrm.DataAccess.Common.Interfaces;
using DataOrm.DataAccess.Common.Threading;

namespace DataOrm.DataAccess.Communication.Threading
{
    public class DataAsyncResult<T> : BasicAsyncResult
    {
        public DataAsyncResult(AsyncCallback callback, object asyncState)
            : base(callback, asyncState)
        {
            Result = new List<T>();
        }

        public List<T> Result { get; set; }
        public IAsyncDataAccess Command { get; set; }
        public object InternalState { get; set; }
    }
}