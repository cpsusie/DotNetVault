using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryStatusVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public sealed class TaskBeganEventArgs : EventArgs, IEquatable<TaskBeganEventArgs>, IComparable<TaskBeganEventArgs>
    {
        #region Public Properties
        public ref readonly Guid TaskId => ref _taskId;
        public DateTime Timestamp { get; }
        public CommandIds CommandId { get; }
        public TaskType TypeOfTask { get; } 
        #endregion

        #region CTOR
        public TaskBeganEventArgs([NotNull] LaundryMachineTaskBase task) : this(TimeStampSource.Now, task) { }

        public TaskBeganEventArgs(DateTime timestamp, [NotNull] LaundryMachineTaskBase task)
        {
            Timestamp = timestamp;
            TypeOfTask = (task ?? throw new ArgumentNullException(nameof(task))).TaskType;
            CommandId = task.MyCommandId;
            _taskId = task.TaskId;
        } 
        #endregion

        #region Public Methods and Operators
        public bool Equals(TaskBeganEventArgs other) =>
            other != null && other.TaskId == TaskId && other.Timestamp == Timestamp &&
            other.CommandId == CommandId && other.TypeOfTask == TypeOfTask;
        public override string ToString() =>
            $"At [{Timestamp:O}], task type [{TypeOfTask.ToString()}] associated " +
            $"with command [{CommandId.ToString()}] started with unique id: [{TaskId.ToString()}]";
        public override int GetHashCode() => TaskId.GetHashCode();
        public override bool Equals(object other) => Equals(other as TaskBeganEventArgs);
        public static bool operator !=(TaskBeganEventArgs lhs, TaskBeganEventArgs rhs) => !(lhs == rhs);

        public static bool operator ==(TaskBeganEventArgs lhs, TaskBeganEventArgs rhs)
        {
            bool? refCompareRes = RefPreCheckEqualityUtils.PreCheckRefEquality(lhs, rhs).ToBoolean();
            // ReSharper disable once PossibleNullReferenceException -- guaranteed not null if non null bool returned above
            return refCompareRes ?? lhs.Equals(rhs);
        }

        public int CompareTo(TaskBeganEventArgs other)
        {
            if (other == null) return -1;
            int tsCompare = Timestamp.CompareTo(other.Timestamp);
            return tsCompare == 0 ? TaskId.CompareTo(other.TaskId) : tsCompare;
        } 
        #endregion

        #region Private Data
        private readonly Guid _taskId; 
        #endregion
    }
    public abstract class LaundryMachineTaskBase : IDisposable
    {
        public CommandIds MyCommandId { get; }
        public TaskType TaskType { get; }
        public ref readonly Guid TaskId => ref _taskId;
        [NotNull] public BasicVault<TaskResult> TaskResult => _resultVault.Value;
        public TaskExecutionStatus ExecutionStatus => _threadStatus;
        
        public event EventHandler<TaskResultEndedEventArgs> TaskCompleted;
        public event EventHandler<TaskBeganEventArgs> TaskBegan;
        protected virtual TaskResultEndedEventArgs CreateTaskResultCompletedEventArgs(in TaskResult res) =>
            new TaskResultEndedEventArgs(in res);
        protected virtual void PerformPreProcessTaskResultActions(TaskResultEndedEventArgs e) { }
        protected virtual void PerformPostProcessTaskResultActions(TaskResultEndedEventArgs e) { }
        [NotNull] protected LaundryStatusVault LaundryFlags => _statusFlagsVault;
        protected TaskFunctionControlBlock<TaskResult> ToControlBlock() =>
            TaskFunctionControlBlock<TaskResult>.CreateControlBlock(in TaskId, ExecutionBlock,
                TokenSource.Token, LaundryCode.TaskResult.CreateNewTask(TaskType), OnFinished);
        protected Stopwatch HighPrecisionTimer => TheStopWatch.Value;
        [NotNull] protected CancellationTokenSource TokenSource => _cts;



        protected virtual BasicVault<TaskResult> InitInitialTaskResult() =>
            new BasicVault<TaskResult>(default, MaxTimeToDispose);
        protected virtual TimeSpan MaxTimeToDispose => TimeSpan.FromSeconds(10);

        protected LaundryMachineTaskBase([NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext,
            [NotNull] LaundryStatusVault statusFlagsVault, [NotNull] IEventRaiser eventRaiser, TaskType type,
            CommandIds myCommandId)
        {
            if (!ReadOnlyFlatEnumSet<TaskType>.AllDefinedEnumValues.Contains(type))
                throw new InvalidEnumArgumentException(nameof(type), (int) type, typeof(TaskType));
            _taskId = Guid.NewGuid();
            Debug.WriteLine($"Created task id with guid: [{_taskId.ToString()}]");
            _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
            _statusFlagsVault = statusFlagsVault ?? throw new ArgumentNullException(nameof(statusFlagsVault));
            _resultVault = new LocklessLazyWriteOnce<BasicVault<TaskResult>>(InitInitialTaskResult);
            _eventRaiser = eventRaiser ?? throw new ArgumentNullException(nameof(eventRaiser));
            TaskType = type;
            if (_executionContext.IsDisposed || _executionContext.IsFaulted)
            {
                throw new ArgumentException($"The execution context provided is not in a useable state.");
            }

            _executionContext.Disposed += _executionContext_Disposed;
            _executionContext.Faulted += _executionContext_Faulted;
            _executionContext.Terminated += _executionContext_Terminated;
            MyCommandId = myCommandId;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~LaundryMachineTaskBase() => Dispose(false);

        protected virtual void OnTaskCompleted(TaskResultEndedEventArgs e)
        {
            if (e != null)
            {
                PostEventToRaiser(Action);
            }

            void Action()=> TaskCompleted?.Invoke(this, e);
        }

        protected virtual void OnTaskBegan(TaskBeganEventArgs e)
        {
            if (e != null)
            {
                PostEventToRaiser(Action);
            }

            void Action() => TaskBegan?.Invoke(this, e);
        }

        private void _executionContext_Terminated(object sender, EventArgs e)
        {
            HandleContextDestructionIfTaskStillPending();
        }

        protected virtual void HandleContextDestructionIfTaskStillPending()
        {
            try
            {
                TaskResult taskResult;
                using (var lck = TaskResult.SpinLock(TimeSpan.FromSeconds(2)))
                {
                    taskResult = lck.Value.WithTerminationResultExplanationAndTimeStamp(TimeStampSource.Now,
                        TaskResultCode.FailedResult, "Execution context destroyed while task still pending.");
                    lck.Value = taskResult;
                }
                OnFinished(taskResult, TaskId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void _executionContext_Faulted(object sender, EventArgs e)
        {
            HandleContextDestructionIfTaskStillPending();
        }

        private void _executionContext_Disposed(object sender, EventArgs e)
        {
            HandleContextDestructionIfTaskStillPending();
        }

        protected void PostEventToRaiser([NotNull] Action a)
        {
            if (_eventRaiser.ThreadActive)
            {
                try
                {
                    _eventRaiser.AddAction(a);
                }
                catch (Exception)
                {
                    //ignore
                }
            }
            else
            {
                try
                {
                    a();
                }
                catch (Exception)
                {
                    //ignore
                }
            }
        }

        protected void OnFinished(TaskResult result, Guid id)
        {
            if (id == TaskId)
            {
                
                TaskResultEndedEventArgs args = CreateTaskResultCompletedEventArgs(in result);
                PerformPreProcessTaskResultActions(args);
                try
                {
                    using var spinLock = LaundryFlags.SpinLock(TimeSpan.FromSeconds(2));
                    ProcessTaskResult(spinLock, args);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    Environment.Exit(-1);
                }
                PerformPostProcessTaskResultActions(args);
                OnTaskCompleted(args);
            }
            else
            {
                Console.Error.WriteLineAsync("Why are we getting a different event's end task handler?");
                Environment.Exit(-1);
            }

        }

        public void CancelTask()
        {
            if (_cancelled.TrySet())
            {
                _cts.Cancel();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                _executionContext.Disposed -= _executionContext_Disposed;
                _executionContext.Terminated -= _executionContext_Terminated;
                _executionContext.Faulted -= _executionContext_Faulted;

                try
                {
                    if (_cancelled.TrySet())
                    {
                        _cts.Cancel();
                    }
                }
                catch (Exception)
                {
                    //ignore
                }

                bool verifiedCompletion = WaitForCompletionStatusOrTimeout(MaxTimeToDispose);
                _cts.Dispose();
                if (!verifiedCompletion)
                {
                    
                    string stackTrace;
                    try
                    {
                        stackTrace = Environment.StackTrace;
                    }
                    catch
                    {
                        stackTrace = string.Empty;
                    }
                    Console.Error.WriteLine($"Unable to verify task completion.  Stack trace: [{stackTrace}]");
                    Environment.FailFast($"Unable to verify completion.  StackTrace: [{stackTrace}].");
                }
            }

            _threadStatus.TryClear();
            _disposed.TrySet();
        }
       
        public void Execute()
        {
            {
                using var lck = TaskResult.SpinLock();
                switch (lck.Value.TerminationStatus)
                {
                    case TaskResultCode.NotStartedResult:
                        if (_started.TrySet())
                        {
                            
                            var controlBlock = ToControlBlock();
                            _executionContext.ExecuteTask(in controlBlock);
                        }
                        else
                        {
                            throw new InvalidOperationException("Task has already been started");
                        }
                        break;
                    case TaskResultCode.ErrorUnknownResult:
                    case TaskResultCode.StillPendingResult:
                    case TaskResultCode.CancelledResult:
                    case TaskResultCode.FailedResult:
                    case TaskResultCode.SuccessResult:
                    default:
                        throw new InvalidOperationException("Task cannot be started given its current state.");
                }
            }
        }

        protected abstract TaskResult ExecuteTask(CancellationTokenPair pair);

        protected TaskResult ExecutionBlock(CancellationTokenPair pair)
        {
            if (_threadStatus.TryStart())
            {
                try
                {
                    pair.ThrowIfCancellationRequested();
                    using (var lck = TaskResult.SpinLock())
                    {
                        lck.Value = LaundryCode.TaskResult.CreateNewTask(TimeStampSource.Now, TaskType);
                    }

                    pair.ThrowIfCancellationRequested();
                    using (var lck = _statusFlagsVault.Lock())
                    {
                        CommandRequestStatus? res =
                            lck.FindCommandById(MyCommandId);
                        if (res?.StatusCode != CommandRequestStatusCode.Pending)
                        {
                            throw new StateLogicErrorException();
                        }
                    }
                    OnTaskBegan(new TaskBeganEventArgs(TimeStampSource.Now, this));
                    return ExecuteTask(pair);
                }
                finally
                {
                    // ReSharper disable once RedundantAssignment
                    bool finished = _threadStatus.TryFinish();
                    Debug.Assert(finished);
                }
            }

            var temp = LaundryCode.TaskResult.CreateNewTask(TimeStampSource.Now, TaskType);
            temp = temp.WithTerminationResultExplanationAndTimeStamp(TimeStampSource.Now,
                TaskResultCode.ErrorUnknownResult, "The task has already been started before.");

            return temp;

        }

        private bool WaitForCompletionStatusOrTimeout(TimeSpan maxTimeout)
        {
            bool verified = false;
            DateTime quitAfter = TimeStampSource.Now + maxTimeout;
            while (!verified && TimeStampSource.Now <= quitAfter)
            {
                verified = _threadStatus.Value != TaskExecutionStatus.Started;

                if (!verified)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }
            }
            return verified;
        }

        protected void SimulateWait(in CancellationTokenPair pair, TimeSpan howLong, string beginMessage = null, string endMessage = null)
        {
            LogMessageIfNotNull(beginMessage);
            try
            {
                DateTime quitAfter = TimeStampSource.Now + howLong;
                while (TimeStampSource.Now <= quitAfter)
                {
                    pair.ThrowIfCancellationRequested();
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }
            }
            finally
            {
                LogMessageIfNotNull(endMessage);
            }
        }

        [Conditional("DEBUG")]
        protected void LogMessageIfNotNull(string beginMessage)
        {
            if (beginMessage != null)
            {
                Debug.WriteLine(beginMessage);
            }
        }

        //protected void _executionContext_TaskComplete(Guid id, TaskResult result)
        //{
        //    if (id == _taskId)
        //    {
        //        TaskResultEndedEventArgs args = new TaskResultEndedEventArgs(in result);
        //        try
        //        {
        //            _eventRaiser.AddAction(() =>
        //            {
        //                TaskResultEndedEventArgs innerArgs = args;
        //                try
        //                {
        //                    ProcessTaskResult(innerArgs);
        //                }
        //                catch (TimeoutException ex)
        //                {
        //                    OnTaskCompleted(HandleFault(ex, id, in result, args));
        //                }
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            OnTaskCompleted(HandleFault(ex, id, in result, args));
        //        }
        //    }

        //    static TaskResultEndedEventArgs HandleFault<TException>(TException ex, Guid lid, in TaskResult lorig,
        //        TaskResultEndedEventArgs lorigA) where TException : Exception
        //    {
        //        string logMessage =
        //            $"An exception of type {ex} occurred after execution of the task [{lid.ToString()}] was completed.  Original result: [{lorig}].  " +
        //            $"Original event args: [{lorigA?.ToString() ?? "NONE"}]. Exception contents: [{ex}].";
        //        TaskResultEndedEventArgs newArgs = new TaskResultEndedEventArgs(lorig.WithTerminationResultExplanationAndTimeStamp(TimeStampSource.Now, TaskResultCode.FailedResult, logMessage));
        //        return newArgs;

        //    }
        //}
        private void ProcessTaskResult(in LockedLaundryStatus lls, in TaskResultEndedEventArgs e)
        {
            switch (e.Result.TerminationStatus)
            {

                default:
                case TaskResultCode.ErrorUnknownResult:
                case TaskResultCode.NotStartedResult:
                case TaskResultCode.StillPendingResult:
                    Console.Error.WriteLineAsync(
                        $"The application must terminate because task result was {e.Result.TerminationStatus.ToString()}");
                    Environment.Exit(-1);
                    break;
                case TaskResultCode.CancelledResult: 
                    lls.CancelMyStatus(MyCommandId, e);
                    bool registered = lls.RegisterCancellationComplete();
                    if (!registered) throw new StateLogicErrorException($"Unable to reset Cancel flag after cancellation of task [{TaskType}]");
                    break;
                case TaskResultCode.FailedResult:
                    lls.FailMyStatus(MyCommandId, e);
                    break;
                case TaskResultCode.SuccessResult:
                    lls.CompleteMyStatus(MyCommandId, e);
                    break;
            }

          
        }


        private LocklessSetOnceFlagVal _cancelled = new LocklessSetOnceFlagVal();
        private TaskExecutionStatusFlag _threadStatus = new TaskExecutionStatusFlag();
        private LocklessSetOnceFlagVal _started = new LocklessSetOnceFlagVal();
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        [NotNull] private readonly LaundryStatusVault _statusFlagsVault;
        [NotNull] private readonly LocklessLazyWriteOnce<BasicVault<TaskResult>> _resultVault;
        private readonly Guid _taskId;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly ILaundryMachineTaskExecutionContext<TaskResult> _executionContext;
        [NotNull] private readonly IEventRaiser _eventRaiser;
        private static readonly ThreadLocal<Stopwatch> TheStopWatch = new ThreadLocal<Stopwatch>(() => new Stopwatch()); 

        
    }


    public enum TaskExecutionStatus
    {
        Clear=0,
        Started=1,
        Finished=2,
    }
    internal struct TaskExecutionStatusFlag
    {
        public static implicit operator TaskExecutionStatus(TaskExecutionStatusFlag tesf) => tesf.Value;

        public TaskExecutionStatus Value
        {
            get
            {
                int code = _code;
                return (TaskExecutionStatus) code;
            }
        }

        public bool TryStart()
        {
            const int wantToBe = (int) TaskExecutionStatus.Started;
            const int needToBeNow = (int) TaskExecutionStatus.Clear;
            return Interlocked.CompareExchange(ref _code, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool TryFinish()
        {
            const int wantToBe = (int)TaskExecutionStatus.Finished;
            const int needToBeNow = (int)TaskExecutionStatus.Started;
            return Interlocked.CompareExchange(ref _code, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool TryClear()
        {
            const int wantToBe = (int)TaskExecutionStatus.Clear;
            const int needToBeNow = (int)TaskExecutionStatus.Finished;
            return Interlocked.CompareExchange(ref _code, wantToBe, needToBeNow) == needToBeNow;
        }
        
        private volatile int _code;
    }

    public readonly struct TaskFunctionControlBlock<[VaultSafeTypeParam] TResult> where TResult : struct, IEquatable<TResult>
    {
        

        public static TaskFunctionControlBlock<TResult> CreateControlBlock(in Guid taskId,
            [NotNull] Func<CancellationTokenPair, TResult> function,
            CancellationToken individualToken, TResult initialStatus, [NotNull] Action<TaskResult, Guid> onFinished) =>
            new TaskFunctionControlBlock<TResult>(function, individualToken, TimeStampSource.Now, initialStatus, in taskId, onFinished);

        public TResult StartingStatus => _startingStatus;
        public DateTime TimeStamp => _submissionTs;
        [NotNull] public Func<CancellationTokenPair, TResult> Function => _func ?? ((CancellationTokenPair pair) => default(TResult));
        public  CancellationToken IndividualToken => _token;
        public Guid Id => _id;
        public Action<TaskResult, Guid> OnFinished =>
            _onFinished ?? ((TaskResult tr, Guid id) =>
            {
                Console.Error.WriteLineAsync($"No valid action supplied on finish. TaskResult: [{tr.ToString()}], task id [{id}].");
                throw new StateLogicErrorException("No valid finish provided.");
            });

        private TaskFunctionControlBlock(Func<CancellationTokenPair, TResult> func,
            CancellationToken individualToken, DateTime ts, TResult initialStatus, in Guid taskId, Action<TaskResult, Guid> onFinished)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
            _token = individualToken;
            _submissionTs = ts;
            _startingStatus = initialStatus;
            _onFinished = onFinished ?? throw new ArgumentNullException(nameof(func));
            _id = taskId;
        }

        private readonly Guid _id;
        private readonly Action<TaskResult, Guid> _onFinished;
        private readonly TResult _startingStatus;
        private readonly DateTime _submissionTs;
        private readonly CancellationToken _token;
        private readonly Func<CancellationTokenPair, TResult> _func;
    }

    public readonly struct CancellationTokenPair : IEquatable<CancellationTokenPair>
    {
        public static ref readonly CancellationTokenPair None => ref TheNoneToken;

        public static CancellationTokenPair CreateTokenPair([NotNull] CancellationTokenSource overallCts) =>
            CreateTokenPair((overallCts ?? throw new ArgumentNullException(nameof(overallCts))).Token,
                CancellationToken.None);

        public static CancellationTokenPair CreateTokenPair(in CancellationToken overall) =>
            CreateTokenPair(in overall, CancellationToken.None);

        public static CancellationTokenPair CreateTokenPair(in CancellationToken overall, in CancellationToken individual) => new CancellationTokenPair(in overall, in individual);

        public CancellationToken OverallToken { get; }

        public CancellationToken IndividualToken { get; }
        
        [Pure]
        public CancellationTokenPair WithNoIndividualToken() => new CancellationTokenPair(OverallToken, CancellationToken.None);
        [Pure]
        public CancellationTokenPair WithSpecifiedIndividualToken(in CancellationToken individual) => new CancellationTokenPair(OverallToken, in individual);

        private CancellationTokenPair(in CancellationToken overall, in CancellationToken individual)
        {
            OverallToken = overall;
            IndividualToken = individual;
        }

        public void ThrowIfCancellationRequested()
        {
            if (OverallToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The overall operation is being cancelled.", OverallToken);
            }

            if (IndividualToken.IsCancellationRequested)
            {
                throw new IndividualOperationCancelledException("The individual operation is being cancelled.", IndividualToken);
            }
        }

        public static bool operator ==(in CancellationTokenPair lhs, in CancellationTokenPair rhs) =>
            lhs.IndividualToken == rhs.IndividualToken && lhs.OverallToken == rhs.OverallToken;
        public static bool operator !=(CancellationTokenPair lhs, CancellationTokenPair rhs) => !(lhs == rhs);
        public override bool Equals(object other) => other is CancellationTokenPair p && p == this;
        public bool Equals(CancellationTokenPair other) => other == this;
        public override int GetHashCode()
        {
            int hash = OverallToken.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ IndividualToken.GetHashCode();
            }
            return hash;
        }

        

        private static readonly CancellationTokenPair TheNoneToken = new CancellationTokenPair(CancellationToken.None, CancellationToken.None);
    }

    public class IndividualOperationCancelledException : OperationCanceledException
    {
        public IndividualOperationCancelledException(string message, Exception innerException, CancellationToken token) : base(message, innerException, token)
        {
        }

        public IndividualOperationCancelledException(string message, CancellationToken token) : base(message, token)
        {
        }
    }

    public static class ActionExtensions
    {
        public static void ExecuteActionLogExceptionIgnore(this Action a)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
            }
        }

        public static void ExecuteActionLogExceptionIgnore<T>(this Action<T> a, T val)
        {
            try
            {
                a(val);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
            }
        }

        public static void ExecuteActionLogExceptionIgnore<T1, T2>(this Action<T1, T2> a, T1 val1, T2 val2)
        {
            try
            {
                a(val1, val2);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
            }
        }
    }

    public static class RefPreCheckEqualityUtils
    {
        public static RefPreCheckEqualityResult PreCheckRefEquality<T>(T lhs, T rhs) where T : class
        {
            if (ReferenceEquals(lhs, rhs)) return RefPreCheckEqualityResult.KnownEqual;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
                return RefPreCheckEqualityResult.KnownNotEqual;
            return RefPreCheckEqualityResult.NeedToCheckValue;
        }

        public static RefPreCheckOrderingResult PreCheckOrder<T>(T lhs, T rhs) where T : class
        {
            if (ReferenceEquals(lhs, rhs)) return RefPreCheckOrderingResult.KnownEqual;
            if (ReferenceEquals(lhs, null)) return RefPreCheckOrderingResult.KnownLessThan;
            if (ReferenceEquals(rhs, null)) return RefPreCheckOrderingResult.KnownGreaterThan;
            return RefPreCheckOrderingResult.NeedToCheckValue;
        }

        public static bool? ToBoolean(this RefPreCheckEqualityResult res)
        {
            bool? ret;
            switch (res)
            {
                case RefPreCheckEqualityResult.KnownEqual:
                    ret = true;
                    break;
                case RefPreCheckEqualityResult.KnownNotEqual:
                    ret = false;
                    break;
                default:
                case RefPreCheckEqualityResult.NeedToCheckValue:
                    ret = null;
                    break;
            }
            return ret;
        }

        public static int? ToIntCompareResult(this RefPreCheckOrderingResult res)
        {
            int? ret;
            switch (res)
            {
                case RefPreCheckOrderingResult.KnownEqual:
                    ret = 0;
                    break;
                case RefPreCheckOrderingResult.KnownLessThan:
                    ret = -1;
                    break;
                case RefPreCheckOrderingResult.KnownGreaterThan:
                    ret = 1;
                    break;
                default:
                case RefPreCheckOrderingResult.NeedToCheckValue:
                    ret = null;
                    break;
            }
            return ret;
        }
    }

    public enum RefPreCheckEqualityResult
    {
        KnownEqual,
        KnownNotEqual,
        NeedToCheckValue
    }

    public enum RefPreCheckOrderingResult
    {
        KnownEqual,
        KnownLessThan,
        KnownGreaterThan,
        NeedToCheckValue
    }


}