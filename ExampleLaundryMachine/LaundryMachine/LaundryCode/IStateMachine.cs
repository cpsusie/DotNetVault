#nullable enable
using System;
using DotNetVault.Attributes;
using DotNetVault.Vaults;

namespace LaundryMachine.LaundryCode
{
    public interface IStateMachine<TFlags, out TFlagVault, [VaultSafeTypeParam] TStateCode> 
        : IDisposable where TFlagVault : IVault<TFlags> where TStateCode : unmanaged, Enum
    {
        event EventHandler? Terminated;
        event EventHandler? Disposed;
        event EventHandler<UnexpectedStateMachineFaultEventArgs>?
            UnexpectedExceptionThrown;
        event EventHandler<StateChangedEventArgs<TStateCode>>? StateChanged;
        event EventHandler<TransitionPredicateTrueEventArgs<TStateCode>>?
            TransitionPredicateTrue;

        ulong StateChangeCount { get; }
        bool IsDisposed { get; }
        bool StateThreadActive { get; }
        bool EventThreadActive { get; }
        bool StartMachineEverCalled { get; }
        BasicVault<LaundryMachineStateCode> StateVault { get; }
        TFlagVault FlagVault { get; }

        void StartStateMachine();
    }
}
#nullable restore