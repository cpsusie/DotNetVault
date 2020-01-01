using System;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public interface IStateTransition<[VaultSafeTypeParam] TStateCode> : IStateTransition where TStateCode : unmanaged, Enum
    {
        new TStateCode OriginState { get; }

        ImmutableArray<TStateCode> DestinationStates { get; }
    }

    public interface IStateTransition
    {
        object OriginState { get; }
        int Priority { get; }
        [NotNull] string TransitionName { get; }
        [NotNull] string PredicateText { get; }
    }
}