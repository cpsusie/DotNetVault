using System;
using System.Collections.Immutable;
using System.Diagnostics;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public delegate bool LTransPredicateEvaluator(in LockedLaundryStatus lls);
    public delegate LaundryMachineStateCode? LTransProcedure(in LockedLaundryStatus lls);
    public delegate void LTransAdditionalProcedure(in LockedLaundryStatus lls, LaundryMachineStateCode? code);

    public abstract class LaundryStateMachineState : StateMachineStateBase<LaundryStatusFlags,
        LaundryMachineStateCode, TaskResultCode, TaskResult, StateMachineStateType, int, LaundryMachineStateTransition, LaundryVault>
    {
        public sealed override StateMachineStateType StateType { get; }
        public event EventHandler<TaskResultEndedEventArgs> TaskEnded;
        public TimeSpan TimeToAddOneUnitDampness { get; }
        public TimeSpan TimeToRemoveOneUnitDirt { get; }
        public TimeSpan TimeToRemoveOneUnitDampness { get; }
        protected LaundryStateMachineState(StateMachineStateType stateType, LaundryMachineStateCode code,
            [NotNull] LaundryVault vault, [NotNull] BasicVault<LaundryMachineStateCode> stateCodeVault,
            [NotNull] IEventRaiser raiser, [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext,
        TimeSpan addOneUnitDamp, TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) :
            base(code, vault, raiser)
        {
            TimeToAddOneUnitDampness = addOneUnitDamp;
            TimeToRemoveOneUnitDirt = removeOneUnitDirt;
            TimeToRemoveOneUnitDampness = removeOneUnitDamp;
            StateType = stateType.IsValueDefined()
                ? stateType
                : throw new ArgumentOutOfRangeException(nameof(stateType), stateType,
                    "Supplied value [{stateType}] is not a defined " +
                    "value of the [{typeof(StateMachineStateType).Name}] enumeration.");
            _context = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
            _stateCodeVault = stateCodeVault ?? throw new ArgumentNullException(nameof(stateCodeVault));
            
        }

        public (LaundryMachineStateCode? NextStateCode, LaundryStateMachineState NextState)
            FindAndExecutePossibleTransition(
                TimeSpan timeout)
        {
            var temp = PerformFindAndExecutePossibleTransition(timeout);
            return (temp.NextStateCode, (LaundryStateMachineState) temp.NextState);
        }

        protected LaundryStateMachineState GetNextState(LaundryMachineStateCode code, TimeSpan addOneWet, TimeSpan removeOneSoil, TimeSpan removeOneDamp) =>
            (LaundryStateMachineState) PerformGetNextState(code);

        protected virtual (LaundryMachineStateCode? NextStateCode, LaundryStateMachineState NextState)
            PerformAdditionalProcessing(in LockedLaundryStatus lls,
                (LaundryMachineStateCode? Value, LaundryStateMachineState NextState) stateInfo) =>
            stateInfo;

        protected sealed override ImmutableSortedSet<LaundryMachineStateTransition> InitTransitionTable()
        {
            var transitions = PerformInitTransitions();
            Debug.Assert(transitions.Length <= 1 || LaundryMachineStateTransition.TransitionComparer.Compare(transitions[0], transitions[1]) != 0);
            var ret = ImmutableSortedSet.Create(LaundryMachineStateTransition.TransitionComparer, transitions);
            Debug.Assert(ret.Count == transitions.Length);
            return ret;
        }

        [NotNull] protected abstract LaundryMachineStateTransition[] PerformInitTransitions();

        protected sealed override (LaundryMachineStateCode? NextStateCode, StateMachineStateBase<LaundryStatusFlags,
                LaundryMachineStateCode, TaskResultCode, TaskResult, StateMachineStateType, int,
                LaundryMachineStateTransition, LaundryVault> NextState)
            PerformFindAndExecutePossibleTransition(TimeSpan timeout)
        {
            ThrowIfDisposed();
            try
            {
                using var lck =
                    FlagVault.Lock(timeout);
                foreach (var item in StateTransitionTable)
                {
                    LaundryMachineStateCode? res = item.ConsiderTransition(in lck);
                    if (res != null)
                    {
                        (LaundryMachineStateCode? Value, LaundryStateMachineState NextState) ret =
                            res.Value != StateCode
                                ? (res.Value, GetNextState(res.Value, TimeToAddOneUnitDampness, TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness))
                                : (res.Value, null);
                        ret = PerformAdditionalProcessing(in lck, ret);
                        return ret;
                    }

                }

                return (null, null);
            }
            catch (TimeoutException)
            {
                OnTimedOutGettingStatusLock();
                return (null, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                return (null, null);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TaskEnded = null;
            }
            base.Dispose(disposing);
        }

        protected virtual void OnTaskEnded(TaskResultEndedEventArgs e)
        {
            if (e != null)
            {
                TaskEnded?.Invoke(this, e);
            }
        }

   

        [NotNull] protected readonly ILaundryMachineTaskExecutionContext<TaskResult> _context;
        [NotNull] protected readonly BasicVault<LaundryMachineStateCode> _stateCodeVault;

    }
}
