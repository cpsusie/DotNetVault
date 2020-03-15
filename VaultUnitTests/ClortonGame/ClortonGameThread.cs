using System;
using System.Threading;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using HpTimesStamps;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace VaultUnitTests.ClortonGame
{
    abstract class ClortonGameThread<T> : ClortonGameThread where T : struct
    {
        public abstract T? Result { get; }
        protected ClortonGameThread([NotNull] BasicReadWriteVault<string> vault, 
            [NotNull] ITestOutputHelper helper) : base(vault, helper)
        {
        }

        
    }

    abstract class ClortonGameThread : IDisposable
    {
        [CanBeNull] public Exception FinishingFault { get; private set; }
        [CanBeNull] public Exception Fault { get; private set; }

        [NotNull] public string LookFor => ClortonGame.LookForText;

        public bool IsDisposed => _disposed.IsSet;
        public TimeSpan Duration => _duration.IsSet ? _duration.Value : TimeSpan.Zero;

        public bool ThreadActive
        {
            get
            {
                var status = _status.Code;
                var started = _startReq.IsSet;
                return (started && status != ThreadStatusCode.Initial && status != ThreadStatusCode.Ended);
            }
        }

        [NotNull] protected Type ConcreteType => _concreteType.Value;

        protected ClortonGameThread([NotNull] BasicReadWriteVault<string> vault, [NotNull] ITestOutputHelper helper)
        {
            _concreteType = new LocklessConcreteType(this);
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));
            _t = new LocklessWriteOnce<Thread>(InitThread);
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public void Begin() => StartThread();

        public void Join()
        {
            if (_startReq.IsSet && _status.Code != ThreadStatusCode.Initial)
            {
                _t.Value.Join();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void StartThread()
        {
            if (_startReq.TrySet())
            {
                _t.Value.Start(_cts.Token);
                DateTime quitAfter = TimeStampSource.Now + TimeSpan.FromMilliseconds(500);
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
            else
            {
                throw new InvalidOperationException("This method may only be called once.");
            }
        }


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

        protected abstract void ExecuteJob(CancellationToken token);

        protected abstract void PerformFinishingActions();

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

        private void DoJob(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_status.TrySetStarted()) throw new InvalidOperationException("Fault starting up.");
            ExecuteJob(token);
        }

        private SetOnceValFlag _startReq = default;

        protected abstract Thread InitThread();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        protected ThreadStatusFlag _status = default;
        private SetOnceValFlag _disposed = default;
        [NotNull] private readonly LocklessWriteOnce<Thread> _t;
        [NotNull] protected readonly ITestOutputHelper _helper;
        [NotNull] protected readonly BasicReadWriteVault<string> _vault;
        [NotNull] private readonly LocklessConcreteType _concreteType;
        [NotNull] private readonly WriteOnce<TimeSpan> _duration = new WriteOnce<TimeSpan>();
    }
}