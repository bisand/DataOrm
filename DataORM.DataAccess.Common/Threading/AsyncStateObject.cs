using System;

namespace DataOrm.DataAccess.Common.Threading
{
    public class AsyncStateObject
    {
        public object State { get; set; }
        public Func<IAsyncResult, object> EndFunction { get; set; }
    }
}