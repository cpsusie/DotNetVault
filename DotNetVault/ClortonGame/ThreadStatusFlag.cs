using System.Threading;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Thread status struct
    /// </summary>
    public struct ThreadStatusFlag
    {
        /// <summary>
        /// Current status of thread.
        /// </summary>
        public ThreadStatusCode Code
        {
            get
            {
                int val = _code;
                return (ThreadStatusCode) val;
            }
        }

        /// <summary>
        /// Try to set to starting state.  Must currently be initial
        /// </summary>
        /// <returns>true success; false fail</returns>
        public bool TrySetStarting() =>
            Set(ThreadStatusCode.Starting, ThreadStatusCode.Initial) == ThreadStatusCode.Initial;

        /// <summary>
        /// Try to set to started, must currently be starting
        /// </summary>
        /// <returns>true success; false fail</returns>
        public bool TrySetStarted() =>
            Set(ThreadStatusCode.Started, ThreadStatusCode.Starting) == ThreadStatusCode.Starting;
        /// <summary>
        /// Try to set to cancel requested, must currently be started
        /// </summary>
        /// <returns>true success; false fail</returns>
        public bool TrySetCancelRequested() => Set(ThreadStatusCode.CancelRequested, ThreadStatusCode.Started) ==
                                               ThreadStatusCode.Started;

        /// <summary>
        /// Set to ended, terminal state
        /// </summary>
        public void ForceEnded() => Interlocked.Exchange(ref _code, (int) ThreadStatusCode.Ended);

        private ThreadStatusCode Set(ThreadStatusCode wantToBe, ThreadStatusCode needToBeNow)
        {
            int wtbInt = (int) wantToBe;
            int ntbInt = (int) needToBeNow;
            int resInt = Interlocked.CompareExchange(ref _code, wtbInt, ntbInt);
            return (ThreadStatusCode) resInt;
        }

        private volatile int _code;
    }

    /// <summary>
    /// status code for threads in the clorton game
    /// </summary>
    public enum ThreadStatusCode
    {
        /// <summary>
        /// initial state
        /// </summary>
        Initial = 0,
        /// <summary>
        /// Start requested and pending
        /// </summary>
        Starting,
        /// <summary>
        /// Thread started and active
        /// </summary>
        Started,
        /// <summary>
        /// Cancel request pending
        /// </summary>
        CancelRequested,
        /// <summary>
        /// Thread ended, terminal state
        /// </summary>
        Ended
    }
}