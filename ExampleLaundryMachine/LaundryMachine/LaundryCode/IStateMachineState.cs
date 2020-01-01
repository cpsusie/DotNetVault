using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public interface IStateMachineState<[VaultSafeTypeParam] TStateCode, [VaultSafeTypeParam] TStateType> where TStateCode : unmanaged, Enum where TStateType : unmanaged, Enum
    {
        event EventHandler<TransitionPredicateTrueEventArgs<TStateCode>> TransitionPredicateTrue;
        event EventHandler TimedOutGettingStatusLock;
        event EventHandler<UnexpectedExceptionDuringTransitionEventArgs> UnexpectedExceptionThrown;
        TStateCode StateCode { get; }
        bool IsDisposed { get; }
        TStateType StateType { get; }
        void RaiseTransitionPredicateTrue(IStateTransition<TStateCode> trans);
    }

    public interface
        IStateMachineState<[VaultSafeTypeParam] TStateCode, [VaultSafeTypeParam] TStateType,
            in TTransition> : IStateMachineState<TStateCode, TStateType> where TStateCode : unmanaged, Enum
        where TStateType : unmanaged, Enum
        where TTransition : IStateTransition<TStateCode>
    {
        void RaiseTransitionPredicateTrue([NotNull] TTransition trans);
    }
}