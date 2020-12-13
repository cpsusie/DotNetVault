using System;
using System.Collections.Concurrent;
using System.Threading;
using DotNetVault.Logging;
using DotNetVault.TimeStamps;
using JetBrains.Annotations;

namespace DotNetVault.TestCaseHelpers
{
    class Executor : IExecutor
    {
        internal static Executor CreateExecutor() => CreateExecutor(DefaultNamePrefix, (str) => new Executor(str));
        internal static Executor CreateExecutor([NotNull] string name) =>
            CreateExecutor(name, str => new Executor(str));
        internal static Executor CreateExecutor([NotNull] Func<string, Executor> ctor) =>
            CreateExecutor(DefaultNamePrefix, ctor);
        internal static Executor CreateExecutor([NotNull] string name, [NotNull] Func<string, Executor> ctor)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (ctor == null) throw new ArgumentNullException(nameof(ctor));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Whitespace-only/empty string prohibited.");
            
            Executor ret = ctor(name);
            try
            {
                ret.Begin();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                try
                {
                    ret.Dispose();
                }
                catch (Exception ex2)
                {
                    Console.Error.WriteLineAsync(ex2.ToString());
                }
                throw;
            }
            return ret;
        }

        public const string DefaultNamePrefix = "ExecutorThread";
        public bool Started => _started.IsSet;
        public bool Faulted => _faulted.IsSet;
        public bool Terminated => _terminated.IsSet;
        public bool IsDisposed => _disposed.IsSet;
        
        protected Executor([NotNull] string namePrefix)
        {
            string threadName = $"{namePrefix}_{(Interlocked.Increment(ref s_sThreadCount))}";
            _t = new Thread(ThreadLoop){IsBackground = true, Name = threadName};
        }

        public void EnqueueAction(Action a)
        {
            if (Started && !Terminated && !IsDisposed && !Faulted)
            {
                _collection.Add(a ?? throw new ArgumentNullException(nameof(a)));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Executor() => Dispose(false);

        protected virtual void StartupActions() { }
        protected virtual void TerminationActions() { }

        private void Begin()
        {
            _startRequested.SetOrThrow();
            _t.Start(_cts.Token);
            DateTime quitAt = DnvTimeStampProvider.MonoLocalNow + TimeSpan.FromMilliseconds(500);
            while (!Started && DnvTimeStampProvider.MonoLocalNow <= quitAt)
            {
                Thread.Sleep(1);
            }
            if (!Started)
            {
                throw new InvalidOperationException("Unable to start thread.");
            }
        }

        private void ThreadLoop(object tokenObj)
        {
            if (tokenObj is CancellationToken ct && _started.TrySet())
            {
                try
                {
                    StartupActions();
                    while (true)
                    {
                        Action a = _collection.Take(ct);
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            a?.Invoke();
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLineAsync(e.ToString());
                        }

                        ct.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    _faulted.TrySet();
                    throw;
                }
                finally
                {
                    _terminated.TrySet();
                    TerminationActions();
                }

            }
            else
            {
                _terminated.TrySet();
                TerminationActions();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed.TrySet() && disposing)
            {
                if (!Terminated)
                {
                    _cts.Cancel();
                }
                _t.Join();
                _cts.Dispose();
                _collection.Dispose();
            }
            _disposed.TrySet();
        }

        [NotNull] private readonly BlockingCollection<Action> _collection = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
        private SetOnceValFlag _startRequested;
        private SetOnceValFlag _faulted;
        private SetOnceValFlag _terminated;
        private SetOnceValFlag _started;
        private SetOnceValFlag _disposed;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly Thread _t;
        private static long s_sThreadCount;
    }
}
