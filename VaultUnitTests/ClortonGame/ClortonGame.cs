using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Logging;
using DotNetVault.TestCaseHelpers;
using DotNetVault.Vaults;
using HpTimesStamps;
using JetBrains.Annotations;

namespace VaultUnitTests.ClortonGame
{
    sealed class ClortonGame : IDisposable
    {
        public static ClortonGame CreateClortonGame([NotNull] IDisposableOutputHelper helper, int numReaders)
        {
            if (helper == null) throw new ArgumentNullException(nameof(helper));
            ClortonGame g = new ClortonGame(helper, numReaders);
            try
            {
                g.Begin();
                DateTime quitAfter = DateTime.Now + TimeSpan.FromMilliseconds(750);
                while (!g.IsStarted && DateTime.Now <= quitAfter)
                {
                    Thread.Sleep(1);
                }

                if (!g.IsStarted)
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
        
        public const string LookForText = "CLORTON";
        public const char XChar = 'x';
        public const char OChar = 'o';

        public event EventHandler<ClortonGameEndedEventArgs> GameEnded;
        public bool IsDisposed => _disposeFlag.IsSet;
        public bool IsStartRequested => _startReq.IsSet;
        public bool IsStarted => _started.IsSet;
        public bool IsCancelled => _cancelled.IsSet;
        public int PendingReaderThreads
        {
            get
            {
                int ret = _notDoneThreadCount;
                return ret;
            }
        }

        private ClortonGame([NotNull] IDisposableOutputHelper outputHelper, int numReaders)
        {
            _success = default;
            _cancelled = default;
            _endTime = null;
            _startTime = default;
            _outputHelper = outputHelper;
            _arbiterThread = new ArbiterThread(_stringVault, outputHelper);
            var readerArrBldr = ImmutableArray.CreateBuilder<ReaderThread>(numReaders);
            int idx = 0;
            while (readerArrBldr.Count < numReaders)
            {
                var rt = new ReaderThread(_stringVault, outputHelper, idx++, LookForText);
                rt.Finished += Rt_Finished;
                readerArrBldr.Add(rt);
            }
            _readerThreads = readerArrBldr.Count == readerArrBldr.Capacity
                ? readerArrBldr.MoveToImmutable()
                : readerArrBldr.ToImmutable();
            _notDoneThreadCount = _readerThreads.Length;
            _xThread = new WriterThread(_stringVault, outputHelper, XChar);
            _oThread = new WriterThread(_stringVault, outputHelper, OChar);

        }

        private void Begin()
        {
            if (TimeStampSource.NeedsCalibration)
            {
                TimeStampSource.Calibrate();
            }
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
        }
        public void Dispose()
        {
            Dispose(true);
        }
        private void Rt_Finished(object sender, ClortonGameFinishedEventArgs e)
        {
            _eventExecutor.EnqueueAction(() => HandleIt(e));
            void HandleIt(ClortonGameFinishedEventArgs finishedArgs)
            {
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
                _xThread.Dispose();
                _oThread.Dispose();
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
                GameEnded?.Invoke(this, args);
            }
        }
        
       


        private void Dispose(bool disposing)
        {
            if (_disposeFlag.TrySet() && disposing)
            {
                _cts.Cancel();
                _cancelled.TrySet();
                GameEnded = null;
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
            GameEnded = null;
            _disposeFlag.TrySet();
        }

        [NotNull] private readonly IDisposableOutputHelper _outputHelper;
        private volatile int _notDoneThreadCount;
        private volatile ClortonGameFinishedEventArgs _winningArgs;
        private SetOnceValFlag _startReq = default;
        private SetOnceValFlag _started = default;
        private SetOnceValFlag _success;
        private SetOnceValFlag _cancelled;
        private DateTime? _endTime;
        private DateTime _startTime;
        [NotNull] private readonly ArbiterThread _arbiterThread;
        private readonly ImmutableArray<ReaderThread> _readerThreads;
        [NotNull] private readonly WriterThread _oThread;
        [NotNull] private readonly WriterThread _xThread;
        private SetOnceValFlag _disposeFlag = default;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly BasicReadWriteVault<string> _stringVault = new BasicReadWriteVault<string>(string.Empty);
        [NotNull] private readonly Executor _eventExecutor =
            Executor.CreateExecutor("ClortonEventExecutor", (str) => new TimeStampCalibratingExecutor(str));

       
    }

    [VaultSafe]
    public sealed class ClortonGameFinishedEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; }
        public bool FoundIt { get; }
        public int ThreadIdx { get; }

        public ClortonGameFinishedEventArgs(DateTime ts, bool foundIt, int idx)
        {
            TimeStamp = ts;
            FoundIt = foundIt;
            ThreadIdx = idx;
        }

        public override string ToString() =>
            $"At [{TimeStamp:O}] thread number {ThreadIdx.ToString()} terminated." + (FoundIt
                ? "It found it."
                : "It did not find it");
    }

    struct ThreadStatusFlag
    {
        public ThreadStatusCode Code
        {
            get
            {
                int val = _code;
                return (ThreadStatusCode) val;
            }
        }

        public bool TrySetStarting() =>
            Set(ThreadStatusCode.Starting, ThreadStatusCode.Initial) == ThreadStatusCode.Initial;

        public bool TrySetStarted() =>
            Set(ThreadStatusCode.Started, ThreadStatusCode.Starting) == ThreadStatusCode.Starting;

        public bool TrySetCancelRequested() => Set(ThreadStatusCode.CancelRequested, ThreadStatusCode.Started) ==
                                               ThreadStatusCode.Started;

        public bool TrySetEndedFromCancel() => Set(ThreadStatusCode.Ended, ThreadStatusCode.CancelRequested) ==
                                               ThreadStatusCode.CancelRequested;

        public bool TrySetEnding() =>
            Set(ThreadStatusCode.Ending, ThreadStatusCode.Started) == ThreadStatusCode.Started;

        public bool TrySetEndedFromEnding() =>
            Set(ThreadStatusCode.Ended, ThreadStatusCode.Ending) == ThreadStatusCode.Ending;

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

    enum ThreadStatusCode
    {
        Initial=0,
        Starting,
        Started,
        CancelRequested,
        Ending,
        Ended
    }
}
