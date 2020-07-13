using System;
using System.Threading;
using DotNetVault.ClortonGame;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Abstract base class for all thread-participants in the DeadBeefCafeBabeGame,
    /// which illustrates the usage of the <see cref="ReadWriteValueListVault{TItem}"/>.
    /// </summary>
    public abstract class CafeBabeGameThread : IDisposable
    {
        /// <summary>
        /// If there is a fault that occurs in the threads finally block, it goes here
        /// </summary>
        [CanBeNull] public Exception FinishingFault { get; private set; }
        /// <summary>
        /// If an exception (other than operation canceled) causes termination of thread,
        /// is stored here
        /// </summary>
        [CanBeNull] public Exception Fault { get; private set; }

        /// <summary>
        /// The number the readers search for (i.e. "dead beef cafe babe")
        /// </summary>
        public ref readonly UInt256 LookFor => ref GameConstants.LookForNumber;
        /// <summary>
        /// True if the thread has been disposed, false otherwise
        /// </summary>
        public bool IsDisposed => _disposed.IsSet;
        /// <summary>
        /// When the thread terminates, its runtime duration goes here.  Until then, Zero
        /// </summary>
        public TimeSpan Duration => _duration.IsSet ? _duration.Value : TimeSpan.Zero;
        /// <summary>
        /// True if the thread is active.  False otherwise.
        /// </summary>
        public bool ThreadActive
        {
            get
            {
                var status = _status.Code;
                var started = _startReq.IsSet;
                return (started && status != ThreadStatusCode.Initial && status != ThreadStatusCode.Ended);
            }
        }

        /// <summary>
        /// Access the concrete type here (for logging)
        /// </summary>
        [NotNull] protected Type ConcreteType => _concreteType.Value;
        /// <summary>
        /// Text representation of the concrete type
        /// </summary>
        [NotNull] protected string ConcreteTypeName => ConcreteType.Name;

        private protected CafeBabeGameThread([NotNull] ReadWriteValueListVault<UInt256> vault, [NotNull] IOutputHelper helper)
        {
            _concreteType = new LocklessConcreteType(this);
            _valueList = vault ?? throw new ArgumentNullException(nameof(vault));
            _t = new LocklessWriteOnce<Thread>(InitThread);
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        /// <summary>
        /// Start the thread
        /// </summary>
        public void Begin() => StartThread();

        /// <summary>
        /// Block the calling thread until this thread ends.
        /// </summary>
        /// <exception cref="ThreadInterruptedException">The thread is interrupted while waiting.</exception>
        public void Join()
        {
            if (_startReq.IsSet && _status.Code != ThreadStatusCode.Initial)
            {
                _t.Value.Join();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Perform whatever task the thread is supposed to do.
        /// </summary>
        /// <param name="token"></param>
        protected abstract void ExecuteJob(CancellationToken token);

        /// <summary>
        /// Perform whatever the thread should do when it is finished.
        /// </summary>
        protected abstract void PerformFinishingActions();

        /// <summary>
        /// Create the thread object
        /// </summary>
        /// <returns></returns>
        protected abstract Thread InitThread();

        /// <summary>
        /// Dispose the thread.
        /// </summary>
        /// <param name="disposing">true if called by code, false if by G.C.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                if (_status.Code != ThreadStatusCode.Ended)
                {
                    _cts.Cancel();
                }

                if (_status.Code != ThreadStatusCode.Initial)
                {
                    _t.Value.Join();
                }

                _cts.Dispose();
            }

            _disposed.TrySet();
        }

        /// <summary>
        /// Call once and only once to start the thread.
        /// </summary>
        /// <exception cref="InvalidOperationException">Already been called another error prevented
        /// thread from starting.</exception>
        protected void StartThread()
        {
            if (_startReq.TrySet())
            {
                try
                {
                    _t.Value.Start(_cts.Token);
                    DateTime quitAfter = CgTimeStampSource.Now + TimeSpan.FromMilliseconds(2000);
                    while (CgTimeStampSource.Now <= quitAfter && _status.Code == ThreadStatusCode.Initial)
                    {
                        Thread.Sleep(1);
                    }

                    if (_status.Code == ThreadStatusCode.Initial)
                    {
                        Dispose(true);
                        throw new InvalidOperationException("Unable to start thread.");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    throw new NotImplementedException();
                    //todo fixit
                    //string status =
                    //    $"Starting up clorton game thread of typ {ConcreteType.Name} " +
                    //    $"with disposed flag set to {_disposed.IsSet}, exception thrown.";
                    //string fault = Fault != null ? $"  Faulting exception: [{Fault}]." : string.Empty;
                    //string wtChar = (this is WriterThread<TVault> wt) ? $"  Writer thread char: [{wt.Char}]." : string.Empty;
                    //string disposeSt = string.Empty;

                    //throw new Exception(status + fault + wtChar + disposeSt, ex);
                }
            }
            else
            {
                throw new InvalidOperationException("This method may only be called once.");
            }
        }


        /// <summary>
        /// the thread loop
        /// </summary>
        /// <param name="tokenObj"></param>
        protected void ThreadLoop(object tokenObj)
        {
            DateTime startedAt;
            CgTimeStampSource.Calibrate();
            if (tokenObj is CancellationToken token && _status.TrySetStarting())
            {
                startedAt = CgTimeStampSource.Now;
                try
                {
                    DoJob(token);
                }
                catch (OperationCanceledException)
                {
                    _status.TrySetCancelRequested();
                }
                catch (Exception ex)
                {
                    Fault = ex;
                }
                finally
                {
                    try
                    {
                        PerformFinishingActions();
                    }
                    catch (Exception ex)
                    {
                        FinishingFault = ex;
                    }

                    _status.ForceEnded();
                    DateTime ts = CgTimeStampSource.Now;
                    TimeSpan duration = ts - startedAt;
                    _duration.TrySet(duration);
                    _helper.WriteLine("At [" + ts.ToString("O") + "], game thread of type [" + ConcreteType.Name +
                                      "] and thread name [" + _t.Value.Name + "] terminated.  Duration: [" +
                                      duration.TotalMilliseconds.ToString("N3") + "] milliseconds.");
                }
            }
        }

        private void DoJob(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_status.TrySetStarted()) throw new InvalidOperationException("Fault starting up.");
            ExecuteJob(token);
        }

        [NotNull] private protected readonly ReadWriteValueListVault<UInt256> _valueList;
        [NotNull] private readonly LocklessConcreteType _concreteType;
        private protected ThreadStatusFlag _status = default;
        private protected SetOnceValFlag _startReq = default;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private protected SetOnceValFlag _disposed = default;
        [NotNull] private readonly LocklessWriteOnce<Thread> _t;
        [NotNull] private protected readonly IOutputHelper _helper;
        [NotNull] private readonly WriteOnce<TimeSpan> _duration = new WriteOnce<TimeSpan>();
        private protected static readonly DeadBeefCafeBabeGameConstants GameConstants = new DeadBeefCafeBabeGameConstants();
    }
}
