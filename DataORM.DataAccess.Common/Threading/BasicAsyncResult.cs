using System;
using System.Diagnostics;
using System.Threading;

namespace DataOrm.DataAccess.Common.Threading
{
    public abstract class BasicAsyncResult : IAsyncResult
    {
        protected readonly AsyncCallback Callback;
        private readonly ManualResetEvent _asyncWaitHandle = new ManualResetEvent(false);
        private IAsyncResult _asyncResultCallback;
        private bool _completedSynchronously;
        private IAsyncResult _internalAsyncResult;
        private string _isCompleted;

        protected BasicAsyncResult(AsyncCallback callback, object asyncState)
        {
            Callback = callback;
            AsyncState = asyncState;
            StopWatch = new Stopwatch();
            StopWatch.Start();
        }

        public Stopwatch StopWatch { get; protected set; }
        public bool TimedOut { get; set; }
        public RegisteredWaitHandle RegisteredWaitHandle { get; set; }
        public Exception Exception { get; set; }

        public AsyncCallback SetCompletedCallback
        {
            get
            {
                return delegate(IAsyncResult asyncResult)
                {
                    _asyncResultCallback = asyncResult;
                    SetCompleted();
                };
            }
        }

        public IAsyncResult InternalAsyncResult
        {
            get { return _asyncResultCallback ?? _internalAsyncResult; }
            set { _internalAsyncResult = value; }
        }

        #region IAsyncResult Members

        public object AsyncState { get; private set; }

        public WaitHandle AsyncWaitHandle
        {
            get { return _asyncWaitHandle; }
        }

        public bool CompletedSynchronously
        {
            get { return _completedSynchronously && IsCompleted; }
            set { _completedSynchronously = value; }
        }

        public bool IsCompleted
        {
            get { return _isCompleted != null; }
        }

        #endregion

        public virtual void SetCompleted()
        {
            StopWatch.Stop();
            if (Interlocked.CompareExchange(ref _isCompleted, string.Empty, null) != null)
                return;

            if (Callback != null)
                Callback(this);

            _asyncWaitHandle.Set();
        }
    }
}