using DotNetVault.RefReturningCollections;
using LaundryStatusVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
namespace LaundryMachine.LaundryCode
{
    public interface ILaundryStateMachine : IStateMachine<LaundryStatusFlags, LaundryStatusVault, LaundryMachineStateCode>
    {
        
    }
}