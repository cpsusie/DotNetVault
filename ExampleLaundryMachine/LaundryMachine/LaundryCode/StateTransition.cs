using System;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public class StateTransition<TStatusFlags, [VaultSafeTypeParam] TStateCode, [VaultSafeTypeParam] TTaskResultCode,
        [VaultSafeTypeParam] TTaskResult, [VaultSafeTypeParam] TStateType, [VaultSafeTypeParam] TStateCodeBacker, TFlagVault> : IStateTransition<TStateCode>, IEquatable<StateTransition<TStatusFlags, TStateCode, TTaskResultCode,
        TTaskResult, TStateType,  TStateCodeBacker, TFlagVault>>, IComparable<StateTransition<TStatusFlags, TStateCode, TTaskResultCode,
        TTaskResult, TStateType, TStateCodeBacker, TFlagVault>> where TStateCode : unmanaged, Enum
        where TTaskResultCode : unmanaged, Enum
        where TTaskResult : struct, IEquatable<TTaskResult>, IComparable<TTaskResult>
        where TStateType : unmanaged, Enum
        where TStatusFlags : class
        where TStateCodeBacker : unmanaged, IEquatable<TStateCodeBacker>, IComparable<TStateCodeBacker>
        where TFlagVault : class, IVault<TStatusFlags>

    {
        public TStateCode OriginState { get; }
        public int Priority { get; }
        public string TransitionName => _name ?? "NULL TRANSITION";
        public string PredicateText => _predicateText ?? "NULL PREDICATE";
        public ImmutableArray<TStateCode> DestinationStates { get; }
        object IStateTransition.OriginState => OriginState;
        protected IStateMachineState<TStateCode, TStateType> OwningState => _owner;
        public static bool operator ==(StateTransition<TStatusFlags, TStateCode, TTaskResultCode,
                TTaskResult, TStateType, TStateCodeBacker, TFlagVault> lhs, 
            StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null)) return false;
            return TheEnumComparer.Equals(lhs.OriginState, rhs.OriginState) && lhs.Priority == rhs.Priority;
        }

        public static bool operator !=( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> lhs,  StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> rhs) => !(lhs == rhs);
        public static bool operator >( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> lhs,  StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> rhs) =>
            Compare(lhs, rhs) > 0;
        public static bool operator <( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> lhs,  StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> rhs) =>
            Compare(lhs, rhs) < 0;
        public static bool operator >=( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> lhs,  StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> rhs) =>
            !(lhs < rhs);
        public static bool operator <=( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> lhs,  StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> rhs) =>
            !(lhs > rhs);

        
        public bool Equals( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> other) => other == this;
        public int CompareTo( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> other) => Compare(this, other);
        public sealed override int GetHashCode()
        {
            int originHash = TheEnumComparer.GetHashCode(OriginState);
            unchecked
            {
                originHash = (originHash * 397) ^ Priority;
            }
            return originHash;
        }

        public sealed override bool Equals(object obj) =>
            obj is StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker,
                TFlagVault> st && st == this;

        protected StateTransition(
            [NotNull]
            IStateMachineState<TStateCode, TStateType> owner, int priority, [NotNull] string name, [NotNull] string predicateText,
            ImmutableArray<TStateCode> destinations)
        {
            OriginState = owner.StateCode;
            Priority = priority;
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            
            _predicateText = predicateText ?? throw new ArgumentNullException(nameof(predicateText));
            
            DestinationStates = (destinations.Length >= 1)
                ? destinations
                : throw new ArgumentException("Parameter must contain at least one value.", nameof(destinations));

        }

        //public TStateCode? ConsiderTransition(
        //    in LockedVaultMutableResource<MutableResourceVault<TStatusFlags>, TStatusFlags> lck)
        //{
        //    if (lck.ExecuteQuery(_transitionPredicate))
        //    {
        //        _owner.OnTransitionPredicateTrue(in this);
        //        var res=  lck.ExecuteMixedOperation(_transitionProcedure);
        //        _additionalProcedure(res);
        //    }
        //    return null;
        //}

        private static int Compare( StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> lhs,  StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault> rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return 0;
            if (ReferenceEquals(lhs, null)) return -1;
            if (ReferenceEquals(rhs, null)) return 1;
            int originComparison = TheEnumComparer.Compare(lhs.OriginState, rhs.OriginState);
            return originComparison == 0 ? lhs.Priority.CompareTo(rhs.Priority) : originComparison;
        }

        private static readonly EnumCompleteComparer<TStateCode, TStateCodeBacker> TheEnumComparer = new EnumCompleteComparer<TStateCode, TStateCodeBacker>();
        private readonly string _predicateText;
        private readonly string _name;
        [NotNull] private readonly IStateMachineState<TStateCode, TStateType> _owner;
    }
}