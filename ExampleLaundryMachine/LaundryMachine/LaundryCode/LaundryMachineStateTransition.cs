using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class LaundryMachineStateTransition : StateTransition<LaundryStatusFlags, LaundryMachineStateCode, TaskResultCode,
        TaskResult, StateMachineStateType, int, LaundryStatusFlagVault>
    {
        public static IComparer<LaundryMachineStateTransition> TransitionComparer { get; } = Comparer<StateTransition<LaundryStatusFlags, LaundryMachineStateCode, TaskResultCode,
            TaskResult, StateMachineStateType, int, LaundryStatusFlagVault>>.Default;

        public LaundryMachineStateTransition([NotNull] IStateMachineState<LaundryMachineStateCode, StateMachineStateType> owner, int priority, [NotNull] string name,
            [NotNull] string predicateText, [NotNull] LTransPredicateEvaluator transitionPredicate,
            [NotNull] LTransProcedure transitionProcedure, [NotNull] LTransAdditionalProcedure additionalProcedure1,
            ImmutableArray<LaundryMachineStateCode> destinations) : base(owner, priority, name, predicateText,
            destinations)
        {
            _transitionPredicate = transitionPredicate ?? throw new ArgumentNullException(nameof(transitionPredicate));
            _transitionProcedure = transitionProcedure ?? throw new ArgumentNullException(nameof(transitionProcedure));
            _additionalProcedure =
                additionalProcedure1 ?? throw new ArgumentNullException(nameof(additionalProcedure1));
        }

        public LaundryMachineStateCode? ConsiderTransition(
            in LockedLsf lck)
        {
            if (_transitionPredicate(in lck))
            {
                OwningState.RaiseTransitionPredicateTrue(this);
                var res = _transitionProcedure(in lck);
                _additionalProcedure(in lck, res);
                return res;
            }
            return null;
        }

        

        [NotNull] private readonly LTransPredicateEvaluator _transitionPredicate;
        [NotNull] private readonly LTransProcedure _transitionProcedure;
        [NotNull] private readonly LTransAdditionalProcedure _additionalProcedure;
    }
}