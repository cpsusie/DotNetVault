using System;
using System.Threading;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using TimeStampSource = DotNetVault.ClortonGame.CgTimeStampSource;
namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// A thread with a value type result object
    /// </summary>
    /// <typeparam name="TResult">The type of the result object</typeparam>
    /// <typeparam name="TVault">The vault type employed by the thread.</typeparam>
    public abstract class ClortonGameThread<TResult, TVault> : ClortonGameThread<TVault> where TResult : struct where TVault : IBasicVault<string>
    {
        /// <summary>
        /// A nullable-struct result object.  Null until available
        /// </summary>
        public abstract TResult? Result { get; }

        /// <inheritdoc />
        protected ClortonGameThread([NotNull] TVault vault, 
            [NotNull] IOutputHelper helper) : base(vault, helper)
        {
        }
    }

    /// <summary>
    /// This serves as the abstract base class for the thread-participants in
    /// the clorton game, which serves as an illustration of the Read Write vault.
    /// </summary>
    public abstract class ClortonGameThread<TVault> : IDisposable where TVault : IBasicVault<string>
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
        /// The text the readers search for (i.e. "CLORTON"
        /// </summary>
        [NotNull]
        public string LookFor { get; } = ClortonGame.GameConstants.LookForText;
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
        /// CTOR
        /// </summary>
        /// <param name="vault">The read write vault</param>
        /// <param name="helper">output helper for logging</param>
        /// <exception cref="ArgumentNullException"><paramref name="vault"/> or <paramref name="helper"/> was null.</exception>
        protected ClortonGameThread([NotNull] TVault vault, [NotNull] IOutputHelper helper)
        {
            _concreteType = new LocklessConcreteType(this);
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));
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
                    DateTime quitAfter = TimeStampSource.Now + TimeSpan.FromMilliseconds(2000);
                    while (TimeStampSource.Now <= quitAfter && _status.Code == ThreadStatusCode.Initial)
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
                    string status =
                        $"Starting up clorton game thread of typ {ConcreteType.Name} " +
                        $"with disposed flag set to {_disposed.IsSet}, exception thrown.";
                    string fault = Fault != null ? $"  Faulting exception: [{Fault}]." : string.Empty;
                    string wtChar = (this is WriterThread<TVault> wt) ? $"  Writer thread char: [{wt.Char}]." : string.Empty;
                    string disposeSt = string.Empty;
                    
                    throw new Exception(status + fault + wtChar + disposeSt, ex);
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
            TimeStampSource.Calibrate();
            if (tokenObj is CancellationToken token && _status.TrySetStarting())
            {
                startedAt = TimeStampSource.Now;
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
                    DateTime ts = TimeStampSource.Now;
                    TimeSpan duration = ts - startedAt;
                    _duration.TrySet(duration);
                    _helper.WriteLine("At [" + ts.ToString("O") + "], game thread of type [" + ConcreteType.Name +
                                      "] and thread name [" + _t.Value.Name + "] terminated.  Duration: [" +
                                      duration.TotalMilliseconds.ToString("N3") + "] milliseconds.");
                }
            }
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
        /// Create the thread object
        /// </summary>
        /// <returns></returns>
        protected abstract Thread InitThread();

        private void DoJob(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_status.TrySetStarted()) throw new InvalidOperationException("Fault starting up.");
            ExecuteJob(token);
        }
        
        /// <summary>
        /// Thread status flag goes here
        /// </summary>
        protected ThreadStatusFlag _status = default;
        private SetOnceValFlag _startReq = default;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private SetOnceValFlag _disposed = default;
        [NotNull] private readonly LocklessWriteOnce<Thread> _t;
        /// <summary>
        /// For output logging
        /// </summary>
        [NotNull] protected readonly IOutputHelper _helper;
        /// <summary>
        /// The read-write vault that stores our string
        /// </summary>
        [NotNull] protected readonly TVault _vault;
        [NotNull] private readonly LocklessConcreteType _concreteType;
        [NotNull] private readonly WriteOnce<TimeSpan> _duration = new WriteOnce<TimeSpan>();
    }
}