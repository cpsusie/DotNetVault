using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    internal sealed class LaundryMachineTaskExecutionContext : ILaundryMachineTaskExecutionContext<TaskResult>
    {
        internal static ILaundryMachineTaskExecutionContext<TaskResult>
            CreateExecutionContext() 
        {
            var ret = new LaundryMachineTaskExecutionContext();
            DateTime quitAfter = TimeStampSource.Now + MaxWaitThreadStart;
            try
            {
                ret._thread.Start(ret._overallCts.Token);
                while (ret._threadStatus.Code == ThreadStatusFlagCode.RequestedThreadStart &&
                       TimeStampSource.Now <= quitAfter)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }

                if (ret._threadStatus.Code != ThreadStatusFlagCode.ThreadStarted)
                {
                    throw new StateLogicErrorException("Unable to start the thread in time.");
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                try
                {
                    ret.Dispose();
                }
                catch (Exception e2)
                {
                    Console.Error.WriteLineAsync(e2.ToString());
                }
                throw;
            }
            return ret;
        }

        public event EventHandler Disposed;
        public event EventHandler Faulted;
        public event EventHandler Terminated;
        public long Id => _id;
        public bool IsTaskBeingProcessedNow => _taskBeingProcessed;
        public bool IsFaulted => _faulted.IsSet;
        public bool IsActive => _threadStatus.Code == ThreadStatusFlagCode.ThreadStarted;
        public bool IsTerminated => _threadStatus.Code == ThreadStatusFlagCode.ThreadTerminated;
        public bool IsDisposed => _disposed.IsSet;
        
        public static readonly TimeSpan MaxWaitThreadStart = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan MaxWaitGetLock = TimeSpan.FromSeconds(2);

        private LaundryMachineTaskExecutionContext()
        {
            _id = Interlocked.Increment(ref _sCount);
            _completionEventRaiser =  EventRaisingThread.CreateEventRaiser($"LmTaskExCtxtEvntRsr{_id}");
            _thread = new Thread(ThreadLoop);
            _thread.IsBackground = true;
            _thread.Name = $"LaundryMachineTaskThread{_id}";
            _thread.Priority = ThreadPriority.Normal;
            if (!_threadStatus.TrySetInstantiated() || !_threadStatus.TrySetRequestedThreadStart())
            {
                throw new StateLogicErrorException("Unable to setup the status flags.");
            }
        }

        public void Dispose() => Dispose(true);

        public void ExecuteTask(in TaskFunctionControlBlock<TaskResult> executeMe)
        {
            ThrowIfNotActiveOrDisposed();
            _collection.Add(executeMe);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                if (_threadStatus.Code != ThreadStatusFlagCode.ThreadTerminated)
                {
                    ActionExtensions.ExecuteActionLogExceptionIgnore(() => _overallCts.Cancel());
                    DateTime quitAfter = TimeStampSource.Now + MaxWaitThreadStart;
                    while (_threadStatus.Code != ThreadStatusFlagCode.ThreadTerminated &&
                           TimeStampSource.Now <= quitAfter)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    }

                    if (_threadStatus.Code != ThreadStatusFlagCode.ThreadTerminated)
                    {
                        Console.Error.WriteLineAsync("Unable to stop the thread within allotted time.");
                    }
                }
                ActionExtensions.ExecuteActionLogExceptionIgnore(() => _overallCts.Dispose());
                ActionExtensions.ExecuteActionLogExceptionIgnore(() => _collection.Dispose());
                _completionEventRaiser.Dispose();
                Terminated = null;
                Faulted = null;
                OnDisposed();
                Disposed = null;
            }
            _disposed.TrySet();
        }

        private void OnDisposed()
        {
            ((Action) Action).ExecuteActionLogExceptionIgnore();
            void Action() => Disposed?.Invoke(this, EventArgs.Empty);
        }

        [Conditional("DEBUG")]
        private void LogBeginTaskGuid(in TaskFunctionControlBlock<TaskResult> func)
        =>
            Debug.WriteLine($"Execution context BEGIN execution of task with id: [{func.Id.ToString()}]");
        [Conditional("DEBUG")]
        private void LogEndTaskGuid(in TaskFunctionControlBlock<TaskResult> func)
            =>
                Debug.WriteLine($"Execution context END execution of task with id: [{func.Id.ToString()}]");


        private void ThreadLoop(object overallCancelObject)
        {
            if (overallCancelObject is CancellationToken overallToken && _threadStatus.TrySetThreadStarted())
            {
                try
                {
                    CancellationTokenPair pair = CancellationTokenPair.CreateTokenPair(overallToken);
                    while (true)
                    {
                        pair.ThrowIfCancellationRequested();
                        bool gotIt = _collection.TryTake(out TaskFunctionControlBlock<TaskResult> func,
                            Timeout.Infinite, pair.OverallToken);
                        if (gotIt)
                        {
                            _taskBeingProcessed.SetOrThrow();
                            LogBeginTaskGuid(in func);
                            try
                            {
                                pair = pair.WithSpecifiedIndividualToken(func.IndividualToken);
                                TaskResult status = func.StartingStatus;
                                try
                                {
                                    status = func.Function(pair);
                                }
                                catch (IndividualOperationCancelledException)
                                {
                                    status = status.WithTerminationResultAndExplanation(TaskResultCode.CancelledResult,
                                        "The task was cancelled at the user's request.");
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    status = status.WithTerminationResultExplanationAndTimeStamp(TimeStampSource.Now,
                                        TaskResultCode.FailedResult,
                                        $"An exception of type [{ex.GetType().Name}] with message [{ex.Message}] faulted the task.  Contents: [{ex}].");
                                }
                                finally
                                {
                                    pair = pair.WithNoIndividualToken();
                                }
                                LogEndTaskGuid(in func);
                                _completionEventRaiser.AddAction(() => func.OnFinished(status, func.Id));
                            }
                            finally
                            {
                                _taskBeingProcessed.ClearOrThrow();
                            }
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Debug.WriteLine($"Cancelling execution context now: [{ex}]");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    if (_faulted.TrySet())
                    {
                        OnFaulted();
                    }
                }
                finally
                {
                    if (_threadStatus.TrySetThreadTerminated())
                    {
                        OnTerminated();
                    }
                    _taskBeingProcessed.ForceClear();
                }
            }
            else
            {
                if (_faulted.TrySet())
                {
                    OnFaulted();
                }
                if (_threadStatus.TrySetThreadTerminated())
                {
                    OnTerminated();
                }
                throw new StateLogicErrorException(
                    "An error occured when trying to start the LaundryMachineTask execution context: bad cancellation token or the thread is in an invalid state.");
            }
            _threadStatus.ForceTerminate();
        }

        private void OnFaulted()
        {
            ((Action) Action).ExecuteActionLogExceptionIgnore();
            void Action() => Faulted?.Invoke(this, EventArgs.Empty);
        }
        private void OnTerminated()
        {
            ((Action)Action).ExecuteActionLogExceptionIgnore();
            void Action() => Terminated?.Invoke(this, EventArgs.Empty);
        }
        private void ThrowIfNotActiveOrDisposed([CallerMemberName] string callerName = "")
        {
            if (_disposed.IsSet)
            {
                throw new ObjectDisposedException(nameof(LaundryMachineTaskExecutionContext),
                    $"Illegal call to {nameof(LaundryMachineTaskExecutionContext)}'s {callerName ?? "UNKNOWN"} " +
                    "member: the object has been disposed.");
            }

            if (_threadStatus.Code != ThreadStatusFlagCode.ThreadStarted)
            {
                throw new InvalidOperationException(
                    $"Illegal call to {nameof(LaundryMachineTaskExecutionContext)}'s {callerName ?? "UNKNOWN"} " +
                    $"member: the object's thread is not in a useable state.  Current state: [{_threadStatus.Code}]");
            }
        }


        private readonly BlockingCollection<TaskFunctionControlBlock<TaskResult>> _collection =
            new BlockingCollection<TaskFunctionControlBlock<TaskResult>>(
                new ConcurrentQueue<TaskFunctionControlBlock<TaskResult>>());
        private LocklessSetOnceFlagVal _faulted = new LocklessSetOnceFlagVal();
        private  ThreadStatusFlag _threadStatus = new ThreadStatusFlag();
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        [NotNull] private readonly CancellationTokenSource _overallCts = new CancellationTokenSource();
        [NotNull] private readonly Thread _thread;
        [NotNull] private readonly IEventRaiser _completionEventRaiser;
        private readonly long _id;
        private LocklessToggleFlag _taskBeingProcessed = new LocklessToggleFlag();
        private static long _sCount;

    }

    internal struct LocklessToggleFlag
    {
        public static implicit operator bool(LocklessToggleFlag ltf) => ltf.IsSet;

        public bool IsSet
        {
            get
            {
                var code = _code;
                return code == IsSetValue;
            }
        }
        public bool IsClear => !IsSet;

        public bool TrySet()
        {
            const int wantToBe = IsSetValue;
            const int needToBeNow = NotSetValue;
            return Interlocked.CompareExchange(ref _code, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool TryClear()
        {
            const int wantToBe = NotSetValue;
            const int needToBeNow = IsSetValue;
            return Interlocked.CompareExchange(ref _code, wantToBe, needToBeNow) == needToBeNow;
        }

        public void SetOrThrow()
        {
            if (!TrySet()) throw new InvalidOperationException("Flag was already set.");
        }

        public void ClearOrThrow()
        {
            if (!TryClear()) throw new InvalidOperationException("Flag was already clear.");
        }

        public void ForceClear()
        {
            Interlocked.Exchange(ref _code, NotSetValue);
        }

        private volatile int _code;
        private const int NotSetValue = 0;
        private const int IsSetValue = 1;

    }
}