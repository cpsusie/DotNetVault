using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public abstract class LaundryMachineTaskBasedStateBase<TLaundryTask> : LaundryStateMachineState
        where TLaundryTask : LaundryMachineTaskBase
    {
        public CommandIds CommandId { get; }
        public virtual TimeSpan MaxTimeToStartOrStop => TimeSpan.FromSeconds(5);
        [CanBeNull] protected TLaundryTask LaundryTask => _laundryTask.IsSet ? _laundryTask.Value : null;
        
        protected LaundryMachineTaskBasedStateBase(LaundryMachineStateCode code,
            CommandIds commandIds, [NotNull] IEventRaiser raiser,
            [NotNull] BasicVault<LaundryMachineStateCode> stateVault, [NotNull] LaundryVault vault,
            [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext,
            ImmutableArray<LaundryMachineStateCode> nextStatesOnCompletion, TimeSpan addOneUnitDamp, TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            StateMachineStateType.WaitForTaskComplete, code, vault, stateVault, raiser, executionContext, addOneUnitDamp, removeOneUnitDirt, removeOneUnitDamp)
        {
            if (stateVault == null) throw new ArgumentNullException(nameof(stateVault));
            if (nextStatesOnCompletion.IsDefault || nextStatesOnCompletion.IsEmpty ||
                nextStatesOnCompletion.Contains(code))
                throw new ArgumentException(
                    $@"Parameter must not be default, empty or contain the value passed by the {nameof(code)} parameter.",
                    nameof(nextStatesOnCompletion));
            if (executionContext == null) throw new ArgumentNullException(nameof(executionContext));
            if (!executionContext.IsActive || executionContext.IsDisposed)
                throw new ArgumentException(@"The execution context is not in a useable state.",
                    nameof(executionContext));
            if (code.GetStateTaskType() == TaskType.NullTask)
            {
                throw new ArgumentException("This state must be a task-based state.");
            }

            CommandId = commandIds;
            _taskEndedTransProcedure = new LocklessLazyWriteOnce<LTransProcedure>(InitTaskEndedTransProcedure);
            _taskEndedAdditionalTransProcedure =
                new LocklessLazyWriteOnce<LTransAdditionalProcedure>(InitTaskEndedAdditionalTransProc);
            _taskEndedTransition =
                new LocklessLazyWriteOnce<LaundryMachineStateTransition>(() =>
                    InitTaskEndedTransition(nextStatesOnCompletion));
            _cancellationTransition =
                new LocklessLazyWriteOnce<LaundryMachineStateTransition>(() =>
                    InitCancellationTransition(code, commandIds));
        }

        public sealed override void Begin()
        {
            PerformPreBeginActions();
            StartTask();
            PerformPostBeginActions();
        }

        protected virtual void PerformPreBeginActions() { }
        protected virtual void PerformPostBeginActions() { }

        [NotNull]
        protected LTransAdditionalProcedure TaskEndedAdditionalProcedure => _taskEndedAdditionalTransProcedure;
        [NotNull] protected LTransProcedure TaskEndedTransProcedure => _taskEndedTransProcedure;

        [NotNull] protected abstract LTransProcedure InitTaskEndedTransProcedure();

        [NotNull]
        protected abstract LTransAdditionalProcedure InitTaskEndedAdditionalTransProc();

        protected virtual LaundryMachineStateCode PerformAdditionalCancellationLogic(in LockedLaundryStatus lls, LaundryMachineStateCode code)
            => StateCode;
     
        protected override void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                TLaundryTask theTask = _laundryTask.IsSet ? _laundryTask.Value : null;
                if (theTask != null)
                {
                    try
                    {
                        theTask.Dispose();
                        DateTime quitAfter = TimeStampSource.Now + MaxTimeToStartOrStop;
                        while (theTask.ExecutionStatus == TaskExecutionStatus.Started && TimeStampSource.Now <= quitAfter)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(1));
                        }

                        if (theTask.ExecutionStatus == TaskExecutionStatus.Started)
                        {
                            Console.Error.WriteLineAsync(
                                $"Unable to stop the task after {MaxTimeToStartOrStop.TotalMilliseconds} milliseconds.");
                            Environment.Exit(-1);
                        }
                    }
                    catch (Exception ex)
                    {
                        string log = ex.ToString();
                        Console.Error.WriteLineAsync(log);
                        Debug.WriteLine(log);
                        base.Dispose(true);
                        Environment.Exit(-1);
                    }
                }
            }
            base.Dispose(disposing);
        }

        public sealed override void ValidateEntryInvariants()
        {
            try
            {
                if (_context.IsTaskBeingProcessedNow) throw new EntryInvariantsNotMetException(StateCode, EntryInvariantDescription);
                using var statusLock = FlagVault.SpinLock();
                bool updatedToPending = statusLock.AcknowledgeMyTask(CommandId);
                if (!updatedToPending)
                {
                    throw new EntryInvariantsNotMetException(StateCode, EntryInvariantDescription);
                }
                ValidateOtherEntryInvariants(in statusLock);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (EntryInvariantsNotMetException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EntryInvariantsNotMetException(StateCode, EntryInvariantDescription, ex);
            }
        }

        protected virtual void StartTask()
        {
            TLaundryTask task = CreateLaundryTask();
            Debug.Assert(task != null);
            _laundryTask.SetOrThrow(task);
            task.TaskCompleted += Task_TaskCompleted;
            task.Execute();
            DateTime quitAfter = TimeStampSource.Now + MaxTimeToStartOrStop;
            while (task.ExecutionStatus == TaskExecutionStatus.Clear && TimeStampSource.Now <= quitAfter)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(1));
            }

            if (task.ExecutionStatus == TaskExecutionStatus.Clear)
            {
                task.Dispose();
                throw new TimeoutException(
                    $"Unable to start task within {MaxTimeToStartOrStop.TotalMilliseconds} " +
                    "milliseconds.");
            }
        }
        protected sealed override LaundryMachineStateTransition[] PerformInitTransitions()
        {
            var b = new List<LaundryMachineStateTransition>(4)
            {
                _taskEndedTransition, _cancellationTransition
            };
            AddAdditionalTransitions(b);
            return b.ToArray();
        }

        protected virtual void AddAdditionalTransitions(
            [NotNull] List<LaundryMachineStateTransition> b)
        {
            
        }

        protected abstract string EntryInvariantDescription { get; }

        protected abstract string ExitInvariantDescription { get; }
        
        public sealed override void EstablishExitInvariants()
        {
            var task = LaundryTask;
            if (task == null) throw new StateLogicErrorException($"Task found to be null when exiting from state {StateCode}.");
            PerformPreResetCurrentCommandActions();
            ResetCurrentCommandAndCancellationStatus();
            PerformPostResetCurrentCommandActions();
        }

       

        protected virtual void PerformPostResetCurrentCommandActions() {}
        
        protected virtual void PerformPreResetCurrentCommandActions()
        {
           
        }

        protected virtual void ValidateOtherEntryInvariants(in LockedLaundryStatus lls)
        {

        }
        
        protected abstract TLaundryTask CreateLaundryTask();


        private void Task_TaskCompleted(object sender, TaskResultEndedEventArgs e) => OnTaskEnded(e);
        



        private string NextStatesOnCompletion(ImmutableArray<LaundryMachineStateCode> nextStates)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            foreach (var c in nextStates)
            {
                sb.Append(c.ToString());
                sb.Append(", ");
            }

            if (sb[^2] == ',' && sb[^1] == ' ')
            {
                sb.Remove(sb.Length - 2, 2);
            }

            sb.Append("}");
            return sb.ToString();
        }

        private void ResetCurrentCommandAndCancellationStatus()
        {
            using var lck = FlagVault.SpinLock(TimeSpan.FromSeconds(2));
            lck.ResetMyStatusAndAnyCancellation(CommandId);
        }
        private LaundryMachineStateTransition InitTaskEndedTransition(
            ImmutableArray<LaundryMachineStateCode> nextStatesOnCompletion) => new LaundryMachineStateTransition(this,
            0,
            $"{StateCode.ToString()} => {NextStatesOnCompletion(nextStatesOnCompletion)}", "The current task ended.",
            (in LockedLaundryStatus lls) => lls.CurrentCommand?.StatusCode != CommandRequestStatusCode.Pending,
            TaskEndedTransProcedure, TaskEndedAdditionalProcedure, nextStatesOnCompletion);

        private LaundryMachineStateTransition InitCancellationTransition(LaundryMachineStateCode code, CommandIds commandIds) =>
            new LaundryMachineStateTransition(this, 1, $"{code.ToString()} => {code.ToString()} (Cancellation Request)",
                "Cancellation has been requested.",
                (in LockedLaundryStatus lls) => lls.CurrentCommand?.CommandId == commandIds &&
                                                lls.CurrentCommand?.StatusCode == CommandRequestStatusCode.Pending &&
                                                lls.CancelFlag.State == CancelState.Requested,
                (in LockedLaundryStatus lls) =>
                {
                    if (!lls.RegisterCancellationPending())
                    {
                        throw new StateLogicErrorException($"Cannot register pending cancellation for [{code}] state.");
                    }

                    return PerformAdditionalCancellationLogic(in lls, code);
                }, (in LockedLaundryStatus lls, LaundryMachineStateCode? stateCode) =>
                {
                    if (stateCode == StateCode)
                    {
                        Debug.Assert(LaundryTask != null);
                        LaundryTask?.CancelTask();
                    }
                }, ImmutableArray.Create(code));

        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        private readonly LocklessWriteOnce<TLaundryTask> _laundryTask = new LocklessWriteOnce<TLaundryTask>();
        
        [NotNull] private readonly LocklessLazyWriteOnce<LaundryMachineStateTransition> _taskEndedTransition;
        [NotNull] private readonly LocklessLazyWriteOnce<LaundryMachineStateTransition> _cancellationTransition;
        [NotNull] private readonly LocklessLazyWriteOnce<LTransProcedure> _taskEndedTransProcedure;
        [NotNull] private readonly LocklessLazyWriteOnce<LTransAdditionalProcedure> _taskEndedAdditionalTransProcedure;


       
    }
}