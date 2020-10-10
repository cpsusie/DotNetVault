using System;
using System.Threading;
using JetBrains.Annotations;
using LockedStressObj = DotNetVault.LockedResources.
    LockedVaultMutableResource<DotNetVault.Vaults.MutableResourceVault<ConsoleStressTest.StressTestObject>,
        ConsoleStressTest.StressTestObject>;

namespace ConsoleStressTest
{

    using MutableResourceVault = DotNetVault.Vaults.MutableResourceVault<StressTestObject>;
    sealed class SimulationThread : IDisposable
    {
        public const int UninitializedThreadId = -1;
        public event EventHandler<ThreadTerminatedEventArgs> Terminated;
        public event EventHandler<ThreadFaultedEventArgs> Faulted; 
        public bool IsDisposed => _disposed.IsSet;
        public bool IsStarted => _started.IsSet;
        public bool IsTerminated => _terminated.IsSet;
        public bool IsFaulted => _faulted.IsSet;
        public int ManagedThreadId => _started.IsSet ? _managedThreadId : UninitializedThreadId;

        public SimulationThread(int numActions, 
            [NotNull] MutableResourceVault vault, [NotNull] LocklessStartSignal startSignal)
        {
            _numActions = numActions > 0
                ? numActions
                : throw new ArgumentOutOfRangeException(nameof(numActions), numActions, @"Value must be positive.");
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));
            _started = startSignal ?? throw new ArgumentNullException(nameof(startSignal));
            _thread = new Thread(ThreadLoop) {Name = $"StressTestThread_{Interlocked.Increment(ref _threadNo)}", IsBackground = false, Priority = ThreadPriority.Normal};
            _thread.Start(_cts.Token);
        }

        public void Dispose() => Dispose(true);

       
        public void Join(CancellationToken token)
        {
            bool sentCancel = false;
            bool terminated = _terminated.IsSet;
            while (!terminated)
            {
                if (token.IsCancellationRequested && !sentCancel)
                {
                    _cts.Cancel();
                    sentCancel = true;
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                terminated = _terminated.IsSet;
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                if (!_terminated.IsSet)
                {
                    _cts.Cancel();
                }

                DateTime waitTil = TimeStampSource.Now + TimeSpan.FromSeconds(2);
                while (TimeStampSource.Now < waitTil && !_terminated.IsSet)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }

                try
                {
                    _cts.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }
            }

            Terminated = null;
            Faulted = null;
            _disposed.TrySet();
        }

        private void ThreadLoop(object cancellationTokenObject)
        {
            if (cancellationTokenObject is CancellationToken token && !_started.IsSet && !_terminated.IsSet &&
                !_disposed.IsSet)
            {
                // ReSharper disable once UnusedVariable
                #pragma warning disable 219 
                const int nonYieldMax = 50; //used in commented out code
                #pragma warning restore 219
                SetManagedThreadId();
                try
                {
                    WaitForStartedOrDisposed(token);
                    int doneActions = 0;
                    //int notYieldedCount = 0;
                    while (doneActions < _numActions)
                    {
                        using (LockedStressObj lck = _vault.SpinLock(token))
                        {
                            lck.RegisterAction(++doneActions);
                            //bug 61 fix following line now rightly causes compiler error
                            //lck.Dispose();
                        }
                        Thread.SpinWait(10000);
                        //Thread.Sleep(TimeSpan.FromMilliseconds(5));
                        
                        //if (doneActions == -1)
                        //{
                        //    token.ThrowIfCancellationRequested();
                        //    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                        //}
                        //else
                        //{
                        //    token.ThrowIfCancellationRequested();

                        //    notYieldedCount += Thread.Yield() ? (-notYieldedCount) : 1;
                        //    if (notYieldedCount > nonYieldMax)
                        //    {
                        //        Thread.Sleep(TimeSpan.FromMilliseconds(250));
                        //        notYieldedCount = 0;
                        //    }
                        //}
                    }
                }
                catch (OperationCanceledException)
                {
                    if (_terminated.TrySet())
                    {
                        Console.Error.WriteLineAsync($"Thread {_thread.Name} terminated due to cancellation.");
                        OnTerminated();
                    }
                }
                catch (Exception ex)
                {
                    DateTime ts = TimeStampSource.Now;
                    if (_faulted.TrySet())
                    {
                        OnFaulted(ts, ex);
                    }
                }
                finally
                {
                    if (_terminated.TrySet())
                    {
                        OnTerminated();
                    }
                }
            }
            else
            {
                try
                {
                    throw new InvalidOperationException("Unable to start thread.");
                }
                catch (InvalidOperationException ex)
                {
                    DateTime ts = TimeStampSource.Now;
                    if (_faulted.TrySet())
                    {
                        OnFaulted(ts, ex);
                    }

                    if (_terminated.TrySet())
                    {
                        OnTerminated();
                    }
                }
                
            }
        }

        private void OnFaulted(DateTime ts, [NotNull] Exception exception)
        {
            try
            {
                ThreadFaultedEventArgs args = new ThreadFaultedEventArgs(_thread.Name ?? "Unnamed Thread", exception, ManagedThreadId, ts);
                Faulted?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
            }
        }

        private void WaitForStartedOrDisposed(in CancellationToken token)
        {
            while (!_started.IsSet && !_disposed.IsSet)
            {
                token.ThrowIfCancellationRequested();
            }
        }

        private void OnTerminated()
        {
            try
            {
                DateTime ts = TimeStampSource.Now;
                Terminated?.Invoke(this,
                    new ThreadTerminatedEventArgs(_thread.Name ?? "Unnamed Thread", ManagedThreadId, ts));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
            }
        }

        private void SetManagedThreadId()
        {
            int wantsToBe = Thread.CurrentThread.ManagedThreadId;
            const int needsToBeNow = UninitializedThreadId;
            int res = Interlocked.CompareExchange(ref _managedThreadId, wantsToBe, needsToBeNow);
            if (res != needsToBeNow)
            {
                throw new InvalidOperationException("Unable to set managed thread id.");
            }
        }

        [NotNull] private readonly MutableResourceVault _vault;
        private readonly int _numActions;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        [NotNull] private readonly LocklessStartSignal _started;
        private LocklessSetOnceFlagVal _terminated = new LocklessSetOnceFlagVal();
        private LocklessSetOnceFlagVal _faulted = new LocklessSetOnceFlagVal();
        private volatile int _managedThreadId=UninitializedThreadId;
        [NotNull] private readonly Thread _thread;
        private static volatile int _threadNo;

       
    }

    public sealed class LocklessStartSignal
    {
        public bool IsSet
        {
            get
            {
                int code = _set;
                return code != NotSet;
            }
        }

        public bool TrySet()
        {
            const int wantToBe = Set;
            const int needToBeNow = NotSet;
            return Interlocked.CompareExchange(ref _set, wantToBe, needToBeNow) == needToBeNow;
        }

        public void SetOrThrow()
        {
            bool ok = TrySet();
            if (!ok) throw new InvalidOperationException("The flag has already been set.");
        }

        public override string ToString() =>
            $"{typeof(LocklessStartSignal).Name} -- Status: [{(IsSet ? "SET" : "NOT SET")}]";

        private volatile int _set = NotSet;
        private const int NotSet = 0;
        private const int Set = 1;
    }

    public sealed class ThreadFaultedEventArgs : EventArgs
    {
        [NotNull] public string ThreadName { get; }
        public int ManagedThreadId { get; }
        public DateTime Timestamp { get; }
        [NotNull] public Exception FaultingException { get; }

        public ThreadFaultedEventArgs([NotNull] string threadName, [NotNull] Exception faultingEx, int threadId) : this(
            threadName, faultingEx, threadId, TimeStampSource.Now) {}

        public ThreadFaultedEventArgs([NotNull] string threadName, [NotNull] Exception faultingEx, int threadId,
            DateTime timestamp)
        {
            ThreadName = threadName ?? throw new ArgumentNullException(nameof(threadName));
            ManagedThreadId = threadId;
            Timestamp = timestamp;
            FaultingException = faultingEx ?? throw new ArgumentNullException(nameof(faultingEx));
            _stringRep = new LocklessLazyWriteOnce<string>(GetStringRep);
        }

        public override string ToString() => _stringRep.Value;
        private string GetStringRep() =>
            $"Thread named {ThreadName} (id: {ManagedThreadId.ToString()}) faulted at [{Timestamp:O}] due to exception: [{FaultingException}].";


        [NotNull] private readonly LocklessLazyWriteOnce<string> _stringRep;
    }

    public sealed class ThreadTerminatedEventArgs : EventArgs
    {
        [NotNull] public string ThreadName { get; }
        public int ManagedThreadId { get; }
        public DateTime Timestamp { get; }

        public ThreadTerminatedEventArgs([NotNull] string threadName, int managedThreadId) 
            : this(threadName, managedThreadId, TimeStampSource.Now) { }

        public ThreadTerminatedEventArgs([NotNull] string threadName, int threadId, DateTime timestamp)
        {
            ThreadName = threadName ?? throw new ArgumentNullException(nameof(threadName));
            ManagedThreadId = threadId;
            Timestamp = timestamp;
        }

        public override string ToString() =>
            $"Thread named {ThreadName} (id: {ManagedThreadId.ToString()}) terminated at [{Timestamp:O}].";
    }

    static class LockedStressTestExtensions
    {
        public static void RegisterAction(
            this in LockedStressObj lck, int actionNo) =>
            lck.ExecuteAction((ref StressTestObject sto, in int no) => sto.Register(no), actionNo);

        [NotNull]
        public static Result GetResult(this in LockedStressObj lck) =>
            lck.ExecuteQuery((in StressTestObject sto) => sto.GetResult());
    }
}
