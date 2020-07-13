using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using DotNetVault.ClortonGame;
using DotNetVault.Logging;
using DotNetVault.TestCaseHelpers;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// An implementation of the dead beef cafe game
    /// </summary>
    public class DeadBeefCafeGame : DeadBeefCafeBabeGameBase
    {
        #region Factories
        /// <summary>
        /// Create a game
        /// </summary>
        /// <param name="helper">output helper</param>
        /// <param name="numReaders">number of readers</param>
        /// <param name="handler">optional finished handler</param>
        /// <returns>a game</returns>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> was null.</exception>
        /// <exception cref="InvalidOperationException">Unable to start game.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="numReaders"/> less than 2.</exception>
        public static DeadBeefCafeGame CreateDeadBeefCafeGame([NotNull] IDisposableOutputHelper helper, int numReaders,
            [CanBeNull] EventHandler<DeadBeefCafeGameEndedEventArgs> handler)
        {
            if (helper == null) throw new ArgumentNullException(nameof(helper));
            Func<DeadBeefCafeGame> ctor = () => new DeadBeefCafeGame(helper, numReaders);
            return CreateDeadBeefCafeBabeGame(ctor, handler);
        }

        /// <summary>
        /// Create a dead beef cafe 
        /// </summary>
        /// <param name="ctor">ctor to make the object</param>
        /// <param name="handler">handler to execute when finishes (optional)</param>
        /// <returns>The game</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ctor"/> was null.</exception>
        /// <exception cref="InvalidOperationException">Unable to start game.</exception>
        /// <exception cref="Exception"><paramref name="ctor"/> threw.</exception>
        protected static DeadBeefCafeGame CreateDeadBeefCafeBabeGame([NotNull] Func<DeadBeefCafeGame> ctor, [CanBeNull] EventHandler<DeadBeefCafeGameEndedEventArgs> handler)
        {
            if (ctor == null) throw new ArgumentNullException(nameof(ctor));
            var game = ctor();
            try
            {
                if (handler != null)
                {
                    game.GameEnded += handler;
                }
                game.Begin();
                DateTime quitAfter = DateTime.Now + TimeSpan.FromMilliseconds(5000);
                while (!game.EverStarted && DateTime.Now <= quitAfter)
                {
                    Thread.Sleep(1);
                }

                if (!game.EverStarted)
                {
                    throw new InvalidOperationException("Unable to start game.");
                }
            }
            catch (Exception ex)
            {
                game._outputHelper.WriteLine(ex.ToString());
                game.Dispose();
                throw;
            }

            return game;
        } 
        #endregion

        #region Properties
        /// <inheritdoc />
        public sealed override bool IsDisposed => _disposeFlag.IsSet;
        /// <inheritdoc />
        public sealed override bool StartEverRequested => _startReq.IsSet;
        /// <inheritdoc />
        public sealed override bool EverStarted => _started.IsSet;
        /// <inheritdoc />
        public sealed override bool IsCancelled => _cancelled.IsSet;

        /// <inheritdoc />
        public sealed override int PendingReaderThreads
        {
            get
            {
                int ret = _notDoneThreadCount;
                return ret;
            }
        }
        #endregion

        #region CTOR
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="outputHelper">io output helper</param>
        /// <param name="numReaders">number of reasons</param>
        /// <exception cref="ArgumentOutOfRangeException">at least two readers required</exception>
        /// <exception cref="ArgumentNullException"><paramref name="outputHelper"/> was null.</exception>
        protected DeadBeefCafeGame([NotNull] IDisposableOutputHelper outputHelper, int numReaders)
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
            _outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
            _numReaders = numReaders;
        }
        #endregion

        #region Protected Methods
        /// <inheritdoc />
        protected override ReadWriteValueListVault<UInt256> InitTVault() => new ReadWriteValueListVault<UInt256>(10_000);
        /// <inheritdoc />
        protected override ArbiterThread InitArbiterThread(ReadWriteValueListVault<UInt256> vault,
            IOutputHelper outputHelper) => new ArbiterThread(vault, outputHelper);
        /// <inheritdoc />
        protected override WriterThread InitWriterThread(ReadWriteValueListVault<UInt256> vault,
            IOutputHelper outputHelper, in UInt256 favoriteNumber, WriterThreadBeginToken beginToken) =>
            new WriterThread(vault, outputHelper, in favoriteNumber, beginToken);
        /// <inheritdoc />
        protected override ReaderThread InitReaderThread(ReadWriteValueListVault<UInt256> vault,
            IOutputHelper outputHelper, int index)
            => new ReaderThread(vault, outputHelper, index);

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing">true if called by dispose method,
        /// false if called by GC Finalizer</param>
        protected override void Dispose(bool disposing)
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
                _valueListVault.Dispose();
                _eventExecutor.Dispose();
            }
            _disposeFlag.TrySet();
            base.Dispose(disposing);
        } 
        #endregion

        #region Private and Private Protected Methods
        private protected void Begin()
        {
            //init stuff
            _valueListVault = InitTVault();
            _arbiterThread = InitArbiterThread(_valueListVault, _outputHelper);
            var readerArrBldr = ImmutableArray.CreateBuilder<ReaderThread>(3);

            int idx = 0;
            while (readerArrBldr.Count < _numReaders)
            {
                var rt = InitReaderThread(_valueListVault, _outputHelper, idx++);
                rt.Finished += Rt_Finished;
                readerArrBldr.Add(rt);
            }

            _readerThreads = readerArrBldr.Count == readerArrBldr.Capacity
                ? readerArrBldr.MoveToImmutable()
                : readerArrBldr.ToImmutable();
            _notDoneThreadCount = _readerThreads.Length;
            _xThread = InitWriterThread(_valueListVault, _outputHelper, in XNumber, _beginToken);
            _oThread = InitWriterThread(_valueListVault, _outputHelper, in ONumber, _beginToken);

            CgTimeStampSource.Calibrate();
            _startReq.SetOrThrow();
            _arbiterThread.Begin();
            foreach (var readerThread in _readerThreads)
            {
                readerThread.Begin();
            }
            _startTime = CgTimeStampSource.Now;
            _started.SetOrThrow();
            _xThread.Begin();
            _oThread.Begin();
            Thread.Sleep(TimeSpan.FromMilliseconds(50));
            _beginToken.SignalBegin();
        }

        private void Rt_Finished(object sender, CafeBabeGameFinishedEventArgs e)
        {
            _eventExecutor.EnqueueAction(() => HandleIt(e));
            void HandleIt(CafeBabeGameFinishedEventArgs finishedArgs)
            {
                Debug.WriteLine(finishedArgs.ToString());
                int newCount = Interlocked.Decrement(ref _notDoneThreadCount);
                if (finishedArgs.FoundIt)
                {
                    bool thisThreadWon = Interlocked.CompareExchange(ref _winningArgs, finishedArgs, null) == null;
                    if (thisThreadWon)
                    {
                        int locatedIndex = e.LocatedIndex ?? -1;
                        if (locatedIndex != -1)
                        {
                            Interlocked.CompareExchange(ref _identifiedIndex, locatedIndex, -1);
                        }

                        _endTime = CgTimeStampSource.Now;
                        _outputHelper.WriteLine("Reader thread at idx [" + e.ThreadIdx + "] WINS.  At [" +
                                                e.TimeStamp.ToString("O") + "], this thread found (at index [" + (e.LocatedIndex?.ToString() ?? "UNKNOWN") + "]) the sought number: [" + LookForNumber +
                                                "].  Congratulations to our lucky reader thread!");
                        _success.SetOrThrow();
                        OnFinished(true);
                    }
                    else
                    {
                        _outputHelper.WriteLine("Reader thread at idx [" + e.ThreadIdx + "] found [" + LookForNumber + "] at " +
                                                "[" + e.TimeStamp.ToString("O") + "], but is too late.  Better luck next time.");
                    }

                }
                else
                {
                    _outputHelper.WriteLine("Reader thread at idx [" + e.ThreadIdx + "] couldn't find [" + LookForNumber + "] but terminated at " +
                                            "[" + e.TimeStamp.ToString("O") + "]. Poor sod, taking the dirt nap so soon.");
                }

                if (newCount == 0 && !_success.IsSet)
                {
                    _endTime = CgTimeStampSource.Now;
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
                ImmutableArray<UInt256> finalResultingArray = _valueListVault.DumpContentsToArrayAndClear(TimeSpan.FromSeconds(10));
                (int xCount, int oCount, int? numberFoundAtIdx) = GetFinalHistogram(finalResultingArray);
                int? winningThreadIndex = _winningArgs?.ThreadIdx;
                DeadBeefCafeGameEndedEventArgs args = new DeadBeefCafeGameEndedEventArgs(_startTime,
                    _endTime ?? CgTimeStampSource.Now, _cancelled.IsSet, finalResultingArray, xCount, oCount, numberFoundAtIdx, winningThreadIndex);
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

            static (int XCount, int OCount, int? SoughtIndex) GetFinalHistogram(ImmutableArray<UInt256> arr)
            {
                int xcount = 0;
                int ocount = 0;
                int? soughtIndex = null;
                if (!arr.IsDefaultOrEmpty)
                {
                    ref readonly var xVal = ref GameConstants.XNumber;
                    ref readonly var oVal = ref GameConstants.ONumber;
                    ref readonly var soughtVal = ref GameConstants.LookForNumber;
                    for (int i = 0; i < arr.Length; ++i)
                    {
                        ref readonly var compareMe = ref arr.ItemRef(i);
                        if (compareMe == xVal)
                            ++xcount;
                        else if (compareMe == oVal)
                            ++ocount;
                        else if (compareMe == soughtVal)
                            soughtIndex = i;
                    }
                }
                return (xcount, ocount, soughtIndex);
            }
        }

        
        #endregion
        
        #region Private fields
        [NotNull] private readonly IDisposableOutputHelper _outputHelper;
        private volatile int _notDoneThreadCount;
        private volatile CafeBabeGameFinishedEventArgs _winningArgs;
        private SetOnceValFlag _startReq = default;
        private SetOnceValFlag _started = default;
        private SetOnceValFlag _success;
        private SetOnceValFlag _cancelled;
        private DateTime? _endTime;
        private DateTime _startTime;
        private ArbiterThread _arbiterThread;
        private ImmutableArray<ReaderThread> _readerThreads;
        private WriterThread _xThread;
        private WriterThread _oThread;
        private volatile int  _identifiedIndex = -1;
        [NotNull] private readonly WriterThreadBeginToken _beginToken = new WriterThreadBeginToken();
        private SetOnceValFlag _disposeFlag = default;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private ReadWriteValueListVault<UInt256> _valueListVault;
        [NotNull]
        private readonly Executor _eventExecutor =
            Executor.CreateExecutor("DeadBeefEventExecutor", (str) => new TimeStampCalibratingExecutor(str));
        private readonly int _numReaders;
        #endregion
    }
}
