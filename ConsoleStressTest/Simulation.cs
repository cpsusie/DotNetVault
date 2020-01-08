using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace ConsoleStressTest
{
    internal sealed class StressSimulation : IStressSimulation
    {
        internal static IStressSimulation CreateStressSimulation(int numThreads, int actionsPerThread) => new StressSimulation(numThreads, actionsPerThread);
        
        public event EventHandler<StressSimDoneEventArgs> Done;
        public int ActionsPerThread { get; }
        public int NumberOfThreads { get; }
        public StressSimCode Status => _status.Status;
        public bool IsDisposed => _disposed.IsSet;

        public TimeSpan Duration => _endedAt.TryGetTimeStamp().HasValue && _startedAt.HasValue
            ? _endedAt.TimeStamp - _startedAt.Value
            : TimeSpan.Zero;
        [CanBeNull] public Exception FaultingException => _faultingEx;
        private StressSimulation(int numThreads, int actionsPerThread)
        {
            if (numThreads < 1) throw new ArgumentOutOfRangeException(nameof(numThreads), numThreads, @"Argument must be positive.");
            if (actionsPerThread < 1) throw new ArgumentOutOfRangeException(nameof(actionsPerThread), actionsPerThread, @"Argument must be positive.");
            _eventRaiser = EventRaisingThread.CreateEventRaiser("StressSimEventRaiser");
            
            _vault = MutableResourceVault<StressTestObject>.CreateMutableResourceVault(() => new StressTestObject(),
                TimeSpan.FromSeconds(2));
            var builder = ImmutableArray.CreateBuilder<SimulationThread>(numThreads);
            while (builder.Count < numThreads)
            {
                var thread = new SimulationThread(actionsPerThread, _vault);
                builder.Add(thread);
                thread.Terminated += Thread_Terminated;
                thread.Faulted += Thread_Faulted;
            }
            Debug.Assert(builder.Capacity == builder.Count);
            _threads = builder.MoveToImmutable();
            _simulationThread = new Thread(ExecuteSimulationThread) {Name = "SimulationManagerThread", IsBackground = false};
            if (!_status.TrySetReady())
            {
                _threads.ApplyToAll(thrd => thrd.Dispose());
                _vault.Dispose();
                _eventRaiser.Dispose();
                throw new InvalidOperationException("Unable to set up simulation.");
            }
            ActionsPerThread = actionsPerThread;
            NumberOfThreads = numThreads;
        }

        public void StartSimulation()
        {
            if (_status.TrySetStarting())
            {
                _simulationThread.Start(_cts.Token);
                DateTime quitAfter = TimeStampSource.Now + TimeSpan.FromSeconds(2);
                while (TimeStampSource.Now <= quitAfter && _status.Status == StressSimCode.Starting)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));

                }

                if (_status.Status == StressSimCode.Starting)
                {
                    throw new InvalidOperationException("Unable to confirm start of the simulation!");
                }

            }
            else
            {
                throw new InvalidOperationException("Unable to start the simulation.");
            }
        }

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                if (Status != StressSimCode.Terminated)
                {
                    _cts.Cancel();
                    DateTime quitAfter = TimeStampSource.Now + TimeSpan.FromSeconds(5);
                    while (TimeStampSource.Now <= quitAfter && Status != StressSimCode.Terminated)
                    {
                        Thread.Sleep(10);
                    }
                }
                _cts.Dispose();
                _threads.ApplyToAll(thread => thread.Dispose());
                _eventRaiser.Dispose();
            }

            _disposed.TrySet();
            Done = null;
        }

        private void ExecuteSimulationThread(object cancellationTokenObject)
        {
            _endedAt.SetThreadAffinity();
            DateTime ts = TimeStampSource.Now;
            if (cancellationTokenObject is CancellationToken token && _status.Status == StressSimCode.Starting)
            {
                try
                {
                    _threads.ApplyToAll(thrd => thrd.Begin());
                    _startedAt = ts;
                    if (_status.TrySetStarted())
                    {
                        foreach (var thread in _threads.Reverse())
                        {
                            thread.Join(token);
                        }

                        DateTime ended = TimeStampSource.Now;
                        _endedAt.SetValue(ended);
                        Console.WriteLine($"ALL THREADS COMPLETE AT: [{TimeStampSource.Now:O}].");
                        if (_status.TrySetFinishing())
                        {
                            DateTime beginProcess = TimeStampSource.Now;
                            Console.WriteLine($"Begin procress results at: [{beginProcess:O}]");
                            string results = GetResults();
                            DateTime endProcess = TimeStampSource.Now;
                            Console.WriteLine($"End processing results at: [{endProcess:O}]; Total Process time: [{(endProcess - beginProcess).TotalMilliseconds:F3}] milliseconds.");
                            if (_status.TrySetTerminatedFromFinishing())
                            {
                                OnDone(results);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Unable to finalize simulation.  Results: [{results}].");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Unable to set status to finishing.");
                        }
                        

                    }
                    else
                    {
                        throw new InvalidOperationException("Unable to start all threads!");
                    }

                }
                catch (OperationCanceledException)
                {
                    DateTime ended = TimeStampSource.Now;
                    _endedAt.TrySet(ended);
                    if (_status.ForceFinished())
                    {
                        OnDone("Cancelled");
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref _faultingEx, ex, null);
                    if (_status.ForceFinished())
                    {
                        OnDone(ex.ToString());
                    }
                }
                finally
                {
                    if (_status.ForceFinished())
                    {
                        OnDone("Finished irregularly.");
                    }
                }
            }
            else
            {
                if (_status.ForceFinished())
                {
                    OnDone("Finished irregularly.");
                }
            }
        }

        private string GetResults()
        {
            Result results;
            {
                using var lck = _vault.SpinLock(_cts.Token);
                results = lck.GetResult();
            }
            var res = results.CreateResults(NumberOfThreads, ActionsPerThread);
            StringBuilder sb = new StringBuilder(res.Report.Length * 2);
            sb.Append((res.Success) ? "SIMULATION SUCCEEDED" : "SIMULATION FAILED");
            sb.Append($" after [{Duration.TotalMilliseconds:F3}] milliseconds.{Environment.NewLine}");
            sb.AppendLine();
            sb.Append(res.Report);
            return sb.ToString();

        }

        

        private void OnDone([NotNull] string cancelled)
        {
            DateTime ts = TimeStampSource.Now;
            _endedAt.TrySet(ts);
            _startedAt ??= _endedAt.TimeStamp;
            StressSimDoneEventArgs args = new StressSimDoneEventArgs(_startedAt.Value, _endedAt.TryGetTimeStamp() ?? _startedAt.Value, cancelled);
            _eventRaiser.AddAction(() => Done?.Invoke(this, args));
        }

       
        private void Thread_Faulted(object sender, ThreadFaultedEventArgs e) => _eventRaiser.AddAction(() => Console.WriteLine(e.ToString()));
        private void Thread_Terminated(object sender, ThreadTerminatedEventArgs e) => _eventRaiser.AddAction(() => Console.WriteLine(e.ToString()));
        [NotNull] private readonly Thread _simulationThread;
        [NotNull] private readonly MutableResourceVault<StressTestObject> _vault;
        [NotNull] private readonly IEventRaiser _eventRaiser;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ImmutableArray<SimulationThread> _threads;
        private DateTime? _startedAt;
        [NotNull] private readonly WriteOnceDateTime _endedAt = new WriteOnceDateTime();
        private StressSimStatus _status = default;
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        private volatile Exception _faultingEx;



    }

    public sealed class WriteOnceDateTime
    {
        public int AffineThreadId
        {
            get
            {
                int ret = _threadId;
                return ret;
            }
        }

        public DateTime TimeStamp
        {
            get
            {
                if (Thread.CurrentThread.ManagedThreadId != _threadId) throw new InvalidOperationException("Can only be accessed by affine thread.");
                return _timeStamp ?? DateTime.MinValue;
            }
        }

        public void SetThreadAffinity()
        {
            int wantToBe = Thread.CurrentThread.ManagedThreadId;
            const int needToBeNow = SimulationThread.UninitializedThreadId;
            int result = Interlocked.CompareExchange(ref _threadId, wantToBe, needToBeNow);
            if (result != needToBeNow)
            {
                throw new InvalidOperationException("Thread Affinity Already Set!");
            }
        }

        public bool SetValue(DateTime ts)
        {
            
            if (AffineThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException("Can't access from this thread!");
            }

            if (_timeStamp.HasValue) return false;
            _timeStamp= ts;
            return true;
        }

        public DateTime? TryGetTimeStamp()
        {
            if (AffineThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                return _timeStamp;
            }

            return null;
        }

        public void TrySet(DateTime ts)
        {
            if (AffineThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                return;
            }
            if (_timeStamp.HasValue)
            {
                return;
            }

            _timeStamp = ts;
        }

        private volatile int _threadId=SimulationThread.UninitializedThreadId;
        private DateTime? _timeStamp;
    }
    

    public class StressSimDoneEventArgs : EventArgs
    {
        public TimeSpan ElapsedTime => EndedAt - StartedAt;
        public DateTime StartedAt { get;}
        public DateTime EndedAt { get; }
        [NotNull] public string DoneMessage { get; }
         
        public StressSimDoneEventArgs(DateTime start, DateTime end, [NotNull] string message)
        {
            DoneMessage = message ?? throw new ArgumentNullException(nameof(message));
            StartedAt = start;
            EndedAt = end;
            _stringRep = new LocklessLazyWriteOnce<string>(GetStringRep);
        }

        public override string ToString() => _stringRep.Value;

        private string GetStringRep()=> $"Simulation took {ElapsedTime.TotalMilliseconds:F3} milliseconds.{Environment.NewLine}Results:{Environment.NewLine}[{DoneMessage}]";
        

        private readonly LocklessLazyWriteOnce<string> _stringRep;
    }

    public enum StressSimCode
    {
        Initial = 0,
        Ready,
        Starting,
        Started,
        CancellationRequested,
        Finishing,
        Terminated
    }

    public struct StressSimStatus
    {
        public StressSimCode Status
        {
            get
            {
                int stat = _status;
                return (StressSimCode) stat;
            }
        }

        public bool TrySetReady()
        {
            const int wantsToBe = (int) StressSimCode.Ready;
            const int needToBeNow = (int) StressSimCode.Initial;
            return Interlocked.CompareExchange(ref _status, wantsToBe, needToBeNow) == needToBeNow;
        }

        public bool TrySetStarting()
        {
            const int wantsToBe = (int)StressSimCode.Starting;
            const int needToBeNow = (int)StressSimCode.Ready;
            return Interlocked.CompareExchange(ref _status, wantsToBe, needToBeNow) == needToBeNow;
        }

        public bool TrySetStarted()
        {
            const int wantsToBe = (int)StressSimCode.Started;
            const int needToBeNow = (int)StressSimCode.Starting;
            return Interlocked.CompareExchange(ref _status, wantsToBe, needToBeNow) == needToBeNow;
        }

        public bool TrySetCancellationRequest()
        {
            const int wantsToBe = (int)StressSimCode.CancellationRequested;
            const int needToBeNow = (int)StressSimCode.Started;
            return Interlocked.CompareExchange(ref _status, wantsToBe, needToBeNow) == needToBeNow;
        }

        public bool TrySetFinishing()
        {
            const int wantsToBe = (int) StressSimCode.Finishing;
            const int needToBeNow = (int)StressSimCode.Started;
            return Interlocked.CompareExchange(ref _status, wantsToBe, needToBeNow) == needToBeNow;
        }

        public bool TrySetTerminatedFromFinishing()
        {
            const int wantsToBe = (int)StressSimCode.Terminated;
            const int needToBeNow = (int)StressSimCode.Finishing;
            return Interlocked.CompareExchange(ref _status, wantsToBe, needToBeNow) == needToBeNow;
        }

        public bool TrySetTerminatedFromCancReq()
        {
            const int wantsToBe = (int)StressSimCode.Terminated;
            const int needToBeNow = (int)StressSimCode.CancellationRequested;
            return Interlocked.CompareExchange(ref _status, wantsToBe, needToBeNow) == needToBeNow;
        }

        public bool ForceFinished()
        {
            int oldVal = Interlocked.Exchange(ref _status, (int) StressSimCode.Terminated);
            return oldVal != (int) StressSimCode.Terminated;
        }

        private volatile int _status;
    }

    public static class SynchronizedConsole
    {
        public static void WriteLine(string writeMe)
        {
            lock (TheSyncObject)
            {
                Console.WriteLine(writeMe);
            }
        }

        public static void Write(string writeMe)
        {
            lock (TheSyncObject)
            {
                Console.Write(writeMe);
            }
        }

        public static void WriteError(string writeMe)
        {
            lock (TheSyncObject)
            {
                Console.Error.Write(writeMe);
            }
        }

        public static void WriteLineError(string writeMe)
        {
            lock (TheSyncObject)
            {
                Console.Error.WriteLine(writeMe);
            }
        }

        private static readonly object TheSyncObject = new object();
    }

    public static class EnumerableExtensions
    {
        public static void ApplyToAll<T>([NotNull] this IEnumerable<T> collection, [NotNull] Action<T> applyMe)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (applyMe == null) throw new ArgumentNullException(nameof(applyMe));
            foreach (var item in collection)
            {
                applyMe(item);
            }
        }

        public static void ApplyWhere<T>([NotNull] this IEnumerable<T> collection, [NotNull] Func<T, bool> predicate,
            [NotNull] Action<T> applyMe)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (applyMe == null) throw new ArgumentNullException(nameof(applyMe));
            foreach (var item in collection.Where(predicate))
            {
                applyMe(item);
            }
        }

        public static void ApplyToAll<T>(this ImmutableArray<T> collection, [NotNull] Action<T> applyMe)
        {
            if (applyMe == null) throw new ArgumentNullException(nameof(applyMe));
            ImmutableArray<T>.Enumerator enumerator = collection.GetEnumerator();
            while (enumerator.MoveNext())
            {
                applyMe(enumerator.Current);
            }
        }

        public static void ApplyWhere<T>(this ImmutableArray<T> collection, [NotNull] Func<T, bool> predicate,
            [NotNull] Action<T> applyMe)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (applyMe == null) throw new ArgumentNullException(nameof(applyMe));
            var enumerator = collection.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (predicate(enumerator.Current))
                {
                    applyMe(enumerator.Current);
                }
            }
        }
    }
}
