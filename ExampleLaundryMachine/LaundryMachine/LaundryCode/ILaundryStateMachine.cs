using System;
using DotNetVault.Vaults;
using LaundryStatusVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
namespace LaundryMachine.LaundryCode
{
    public interface ILaundryStateMachine : IDisposable
    {
        event EventHandler Terminated;
        event EventHandler Disposed;
        event EventHandler<UnexpectedStateMachineFaultEventArgs> UnexpectedExceptionThrown;
        event EventHandler<StateChangedEventArgs<LaundryMachineStateCode>> StateChanged;
        event EventHandler<TransitionPredicateTrueEventArgs<LaundryMachineStateCode>> TransitionPredicateTrue;
        ulong StateChangeCount { get; }
        bool IsDisposed { get; }
        bool StateThreadActive { get; }
        bool EventThreadActive { get; }
        bool StartMachineEverCalled { get; }
        BasicVault<LaundryMachineStateCode> StateVault { get; }
        LaundryStatusVault FlagVault { get; }
        void StartStateMachine();
    }
}