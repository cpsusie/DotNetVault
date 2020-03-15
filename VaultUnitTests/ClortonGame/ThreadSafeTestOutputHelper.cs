using System;
using System.Collections.Concurrent;
using System.Threading;
using DotNetVault.Logging;
using HpTimesStamps;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace VaultUnitTests.ClortonGame
{
    sealed class ThreadSafeTestOutputHelper : IDisposableOutputHelper
    {
        public static IDisposableOutputHelper CreateOutputHelper([NotNull] ITestOutputHelper helper)
            => new ThreadSafeTestOutputHelper(helper);

        public void WriteLine(string message)
        {
            if (_started.IsSet && !_terminated.IsSet && !_disposed.IsSet)
            {
                try
                {
                    _collection.Add(() => _helper.WriteLine(message));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                    throw;
                }
            }
        }


        public void WriteLine(string format, params object[] args)
        {
            if (_started.IsSet && !_terminated.IsSet && !_disposed.IsSet)
            {
                try
                {
                    _collection.Add(() => _helper.WriteLine(format, args));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                    throw;
                }
            }
        }

        private ThreadSafeTestOutputHelper([NotNull] ITestOutputHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _thread = new Thread(ThreadLoop){Name = "DisposableOutputHelper", IsBackground = true};
            _thread.Start(_cts.Token);
            DateTime quitAt = TimeStampSource.Now + TimeSpan.FromMilliseconds(500);
            while (!_started.IsSet && TimeStampSource.Now <= quitAt)
            {
                Thread.Sleep(1);
            }

            if (!_started.IsSet)
            {
                throw new InvalidOperationException("Couldn't start thread.");
            }
        }

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (_disposed.TrySet() && disposing)
            {
                if (!_terminated.IsSet && _started.IsSet)
                {
                    _cts.Cancel();
                    _thread.Join();
                }
                _collection.Dispose();
                _cts.Dispose();
            }
        }

        private void ThreadLoop(object tokenObj)
        {
            if (_started.TrySet() && tokenObj is CancellationToken token)
            {
                try
                {
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        Action doMe = _collection.Take(token);
                        if (doMe != null)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                doMe();
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLineAsync(ex.ToString());
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    Console.Error.WriteAsync(ex.ToString());
                    throw;
                }
                finally
                {
                    _terminated.TrySet();
                }
            }
            else
            {
                throw new InvalidOperationException("Thread already started.");
            }
        }

        private SetOnceValFlag _terminated = default;
        private SetOnceValFlag _started = default;
        private SetOnceValFlag _disposed = default;
        [NotNull] private readonly ITestOutputHelper _helper;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly BlockingCollection<Action> _collection = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
        [NotNull] private readonly Thread _thread;
    }
}