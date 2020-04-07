using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using DotNetVault.TestCaseHelpers;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using TimeStampSource = DotNetVault.ClortonGame.CgTimeStampSource;
using GameConstants = DotNetVault.ClortonGame.ClortonGameConstants;
namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// This class provides a "game" that can be used (and is used) as the basis of unit tests
    /// and demonstration of the usage of read write vaults.
    ///
    /// The clorton thread is a game played by reader threads that all contend for access to a protected resource.
    /// The reader threads search an ever growing string for the text "CLORTON".  The first reader thread to detect it and
    /// report that it detected it wins.
    ///
    /// There are also two writer threads: an 'x' writer and an 'o' writer.
    /// These threads acquire the resource and append a random number (between 0 and 5, inclusively) of their respective
    /// 'x's and 'o's then release the resource ... rinse and repeat
    ///
    /// There is also an "arbiter" thread whose job is to determine whether conditions are correct to write "CLORTON" into the buffer.
    /// The arbiter thread
    ///     1- obtains an UPGRADABLE READ-ONLY LOCK
    ///     2- checks for the termination condition
    ///         (the absolute value of the difference between
    ///         the x count and o count is non-zero and evenly divisible by thirteen)
    ///     3- If the termination condition is present, it
    ///         a. upgrades to a write lock
    ///         b. appends "CLORTON"
    ///         c. releases write lock
    ///         d. releases the upgradable readonly lock and
    ///         e. terminates
    ///        If the termination condition is NOT present, it releases its upgradable read-only lock
    ///     4. rinse and repeat until 3.e
    /// 
    /// </summary>
    public abstract class ClortonGame : IClortonGame, IClortonGameConstants
    {
        /// <summary>
        /// static access for game constants
        /// </summary>
        public static GameConstants GameConstants { get; } = new GameConstants();
        /// <inheritdoc />
        public string LookForText => GameConstants.LookForText;
        /// <inheritdoc />
        public char XChar => GameConstants.XChar;
        /// <inheritdoc />
        public char OChar => GameConstants.OChar;
        /// <inheritdoc />
        public abstract bool IsDisposed { get; }
        /// <inheritdoc />
        public abstract bool StartEverRequested { get; }
        /// <inheritdoc />
        public abstract bool EverStarted { get; }
        /// <inheritdoc />
        public abstract bool IsCancelled { get; }
        /// <inheritdoc />
        public abstract int PendingReaderThreads { get; }
        /// <inheritdoc />
        public event EventHandler<ClortonGameEndedEventArgs> GameEnded;
        /// <summary>
        /// Concrete type of Clorton Game
        /// </summary>
        [NotNull]
        protected Type ConcreteType => _concreteType.Value;
        /// <summary>
        /// string text representation of <see cref="ConcreteType"/>
        /// </summary>
        [NotNull] protected string ConcreteTypeName => ConcreteType.Name;

        private protected ClortonGame() => _concreteType = new LocklessConcreteType(this);

        /// <summary>
        /// end the game if running, free resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// dispose
        /// </summary>
        /// <param name="disposing">true if called by code,
        /// false if by gc finalizer</param>
        private protected virtual void Dispose(bool disposing) => GameEnded = null;

        private protected virtual void OnGameEnded([NotNull] ClortonGameEndedEventArgs e)
            => GameEnded?.Invoke(this, e);

        [NotNull] private readonly LocklessConcreteType _concreteType;
       
    }

    
    /// <summary>
    /// Base class for clorton game implementations
    /// </summary>
    /// <typeparam name="TVault">The vault type.  Implementations for <see cref="BasicReadWriteVault{T}"/> (where T is string)
    /// and <see cref="StringBufferReadWriteVault"/> will be provided. </typeparam>
    public abstract class ClortonGameBase<TVault> : ClortonGame where TVault : IBasicVault<string>
    {
        #region Factory Method

        /// <summary>
        /// Create and begin a clorton game.
        /// </summary>
        /// <param name="ctor">ctor for creating derived type</param>
        /// <param name="helper">An output helper (for logging purposes)</param>
        /// <returns>A clorton game</returns>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> was null.</exception>
        /// <exception cref="InvalidOperationException">There was a problem creating or starting the game</exception>
        protected static ClortonGameBase<TVault> CreateClortonGame([NotNull] Func<ClortonGameBase<TVault>> ctor, [NotNull] IDisposableOutputHelper helper) =>
            CreateClortonGame(ctor, helper, null);


        /// <summary>
        /// Create and begin a clorton game.
        /// </summary>
        /// <param name="ctor">ctor for creating derived type</param>
        /// <param name="helper">An output helper (for logging purposes)</param>
        /// <param name="doneHandler">(optional) event handler for when the game finished</param>
        /// <returns>A clorton game</returns>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> was null.</exception>
        /// <exception cref="InvalidOperationException">There was a problem creating or starting the game</exception>
        protected static ClortonGameBase<TVault> CreateClortonGame([NotNull] Func<ClortonGameBase<TVault>> ctor, [NotNull] IDisposableOutputHelper helper, [CanBeNull] EventHandler<ClortonGameEndedEventArgs> doneHandler)
        {
            if (helper == null) throw new ArgumentNullException(nameof(helper));
            ClortonGameBase<TVault> g = ctor();
            try
            {
                if (doneHandler != null)
                {
                    g.GameEnded += doneHandler;
                }

                g.Begin();
                DateTime quitAfter = DateTime.Now + TimeSpan.FromMilliseconds(5000);
                while (!g.EverStarted && DateTime.Now <= quitAfter)
                {
                    Thread.Sleep(1);
                }

                if (!g.EverStarted)
                {
                    throw new InvalidOperationException("Unable to start game.");
                }
            }
            catch (Exception ex)
            {
                helper.WriteLine(ex.ToString());
                g.Dispose();
                throw;
            }
            return g;
        }
        #endregion

        #region Public Properties, events and constants
    

       

        /// <summary>
        /// True if the game has been disposed
        /// </summary>
        public sealed override bool IsDisposed => _disposeFlag.IsSet;
        /// <summary>
        /// True if start has ever been requested
        /// </summary>
        public sealed override bool StartEverRequested => _startReq.IsSet;
        /// <summary>
        /// True if the game ever started
        /// </summary>
        public sealed override bool EverStarted => _started.IsSet;
        /// <summary>
        /// True if the game was cancelled
        /// </summary>
        public sealed override bool IsCancelled => _cancelled.IsSet;
        /// <summary>
        /// Number of pending reader threads
        /// </summary>
        public sealed override int PendingReaderThreads
        {
            get
            {
                int ret = _notDoneThreadCount;
                return ret;
            }
        }
        /// <summary>
        /// The string vault.
        /// </summary>
        [NotNull] protected TVault StringVault => _stringVault;
        /// <summary>
        /// The arbiter thread
        /// </summary>
        [CanBeNull]
        protected ArbiterThread<TVault> ArbiterThread => _arbiterThread;
        /// <summary>
        /// The o writer thread
        /// </summary>
        [NotNull] protected WriterThread<TVault> OWriter => _oThread;
        /// <summary>
        /// The x writer thread
        /// </summary>
        [NotNull] protected WriterThread<TVault> XWriter => _xThread;
        /// <summary>
        /// The reader threads
        /// </summary>
        [ItemNotNull] protected ImmutableArray<ReaderThread<TVault>> ReaderThreads => _readerThreads;
        #endregion

        #region Private CTOR
        private protected ClortonGameBase([NotNull] IDisposableOutputHelper outputHelper, int numReaders)
        {
            if (numReaders < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(numReaders), numReaders,
                    @"At least two readers are required.");
            }
            _success = default;
            _cancelled = default;
            _endTime = null;
            _startTime = default;
            _outputHelper = outputHelper;
            _numReaders = numReaders;


        }
        #endregion
        

        #region abstract methods
        /// <summary>
        /// Initialize the vault
        /// </summary>
        /// <returns>the vault.</returns>
        [NotNull] private protected abstract TVault InitTVault();

        [NotNull]
        private protected abstract ArbiterThread<TVault> InitArbiterThread([NotNull] TVault vault, [NotNull] IOutputHelper outputHelper);
        [NotNull] private protected abstract WriterThread<TVault> InitWriterThread([NotNull] TVault vault,
            [NotNull] IOutputHelper outputHelper, char charToWrite, [NotNull] WriterThreadBeginToken beginToken);
        [NotNull] private protected abstract ReaderThread<TVault> InitReaderThread([NotNull] TVault vault,
            [NotNull] IOutputHelper outputHelper, int index, [NotNull] string lookFor);
        #endregion

        #region Private Methods
        private protected void Begin()
        {
            //init stuff
            _stringVault = InitTVault();
            _arbiterThread = InitArbiterThread(_stringVault, _outputHelper);
            var readerArrBldr = ImmutableArray.CreateBuilder<ReaderThread<TVault>>();
            
            int idx = 0;
            while (readerArrBldr.Count < _numReaders)
            {
                var rt = InitReaderThread(_stringVault, _outputHelper, idx++, LookForText);
                rt.Finished += Rt_Finished;
                readerArrBldr.Add(rt);
            }

            _readerThreads = readerArrBldr.Count == readerArrBldr.Capacity
                ? readerArrBldr.MoveToImmutable()
                : readerArrBldr.ToImmutable();
            _notDoneThreadCount = _readerThreads.Length;
            _xThread = InitWriterThread(_stringVault, _outputHelper, XChar, _beginToken);
            _oThread = InitWriterThread(_stringVault, _outputHelper, OChar, _beginToken);
            
            TimeStampSource.Calibrate();
            _startReq.SetOrThrow();
            _arbiterThread.Begin();
            foreach (var readerThread in _readerThreads)
            {
                readerThread.Begin();
            }
            _startTime = TimeStampSource.Now;
            _started.SetOrThrow();
            _xThread.Begin();
            _oThread.Begin();
            Thread.Sleep(TimeSpan.FromMilliseconds(50));
            _beginToken.SignalBegin();
        }

        private void Rt_Finished(object sender, ClortonGameFinishedEventArgs e)
        {
            _eventExecutor.EnqueueAction(() => HandleIt(e));
            void HandleIt(ClortonGameFinishedEventArgs finishedArgs)
            {
                Debug.WriteLine(finishedArgs.ToString());
                int newCount = Interlocked.Decrement(ref _notDoneThreadCount);
                if (finishedArgs.FoundIt)
                {
                    bool thisThreadWon = Interlocked.CompareExchange(ref _winningArgs, finishedArgs, null) == null;
                    if (thisThreadWon)
                    {
                        _endTime = TimeStampSource.Now;
                        _outputHelper.WriteLine("Reader thread at idx [" + e.ThreadIdx + "] WINS.  At [" +
                                                e.TimeStamp.ToString("O") + "], this thread found [" + LookForText +
                                                "].  Congratulations to our lucky reader thread!");
                        _success.SetOrThrow();
                        OnFinished(true);
                    }
                    else
                    {
                        _outputHelper.WriteLine("Reader thread at idx [" + e.ThreadIdx + "] found [" + LookForText + "] at " +
                                                "[" + e.TimeStamp.ToString("O") + "], but is too late.  Better luck next time.");
                    }

                }
                else
                {
                    _outputHelper.WriteLine("Reader thread at idx [" + e.ThreadIdx + "] couldn't find [" + LookForText + "] but terminated at " +
                                            "[" + e.TimeStamp.ToString("O") + "]. Poor sod, taking the dirt nap so soon.");
                }

                if (newCount == 0 && !_success.IsSet)
                {
                    _endTime = TimeStampSource.Now;
                    OnFinished(false);
                }
            }
        }

        private void OnFinished(bool success)
        {
            _eventExecutor.EnqueueAction(HandleIt);
            void HandleIt()
            {
                if (!success)
                {
                    _cancelled.TrySet();
                }
                _arbiterThread.Dispose();
                WaitForWriterThreadThenDispose(TimeSpan.FromSeconds(5));
                foreach (var thread in _readerThreads)
                {
                    thread.Dispose();
                }
                string finalString = _stringVault.CopyCurrentValue(TimeSpan.FromSeconds(10));
                int xCount = finalString.Count(c => c == XChar);
                int oCount = finalString.Count(c => c == OChar);
                int? winningThreadIndex = _winningArgs?.ThreadIdx;
                ClortonGameEndedEventArgs args = new ClortonGameEndedEventArgs(_startTime,
                    _endTime ?? TimeStampSource.Now, _cancelled.IsSet, finalString, xCount, oCount, winningThreadIndex);
                OnGameEnded(args);
            }

            void WaitForWriterThreadThenDispose(TimeSpan timeout)
            {
                bool xStarted = _xThread.ThreadActive;
                bool oStarted = _oThread.ThreadActive;
                DateTime quitAfter = CgTimeStampSource.Now + timeout;
                while ((!xStarted || !oStarted) && CgTimeStampSource.Now <= quitAfter)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(1));
                    xStarted = _xThread.ThreadActive;
                    oStarted = _oThread.ThreadActive;
                }

                if (xStarted)
                {
                    _xThread.Dispose();
                }
                else
                {
                    try
                    {
                        _xThread.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"X thread was not started when it was disposed and exception thrown: [{ex}].");
                    }
                }

                if (oStarted)
                {
                    _oThread.Dispose();
                }
                else
                {
                    try
                    {
                        _oThread.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"O thread was not started when it was disposed and exception thrown: [{ex}].");
                    }
                }
            }
        }

        private protected sealed override void Dispose(bool disposing)
        {
            if (_disposeFlag.TrySet() && disposing)
            {
                _cts.Cancel();
                _cancelled.TrySet();
                foreach (var thread in _readerThreads)
                {
                    thread.Dispose();
                    thread.Join();
                }
                _xThread.Dispose();
                _xThread.Join();
                _oThread.Dispose();
                _oThread.Join();
                _arbiterThread.Dispose();
                _arbiterThread.Join();

                _cts.Dispose();
                _stringVault.Dispose();
                _eventExecutor.Dispose();
            }
            _disposeFlag.TrySet();
            base.Dispose(disposing);
        } 
        #endregion

        #region Private fields
        [NotNull] private readonly IDisposableOutputHelper _outputHelper;
        private volatile int _notDoneThreadCount;
        private volatile ClortonGameFinishedEventArgs _winningArgs;
        private SetOnceValFlag _startReq = default;
        private SetOnceValFlag _started = default;
        private SetOnceValFlag _success;
        private SetOnceValFlag _cancelled;
        private DateTime? _endTime;
        private DateTime _startTime;
        private ArbiterThread<TVault> _arbiterThread;
        private ImmutableArray<ReaderThread<TVault>> _readerThreads;
        private WriterThread<TVault> _oThread;
        private WriterThread<TVault> _xThread;
        [NotNull] private readonly WriterThreadBeginToken _beginToken = new WriterThreadBeginToken();
        private SetOnceValFlag _disposeFlag = default;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private TVault _stringVault;
        [NotNull]
        private readonly Executor _eventExecutor =
            Executor.CreateExecutor("ClortonEventExecutor", (str) => new TimeStampCalibratingExecutor(str));
        private readonly int _numReaders;
        #endregion


    }
}
