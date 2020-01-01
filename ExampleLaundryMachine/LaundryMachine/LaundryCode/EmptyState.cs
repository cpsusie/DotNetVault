using System;
using System.Collections.Immutable;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class EmptyState : LaundryStateMachineState
    {
        public EmptyState([NotNull] LaundryVault vault,
            [NotNull] BasicVault<LaundryMachineStateCode> stateCodeVault, [NotNull] IEventRaiser raiser,
            [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext, TimeSpan addOneUnitDamp, TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            StateMachineStateType.WaitForMoreInputOnly, LaundryMachineStateCode.Empty, vault, stateCodeVault, raiser, executionContext, addOneUnitDamp, removeOneUnitDirt, removeOneUnitDamp)
        {

        }

        public override void Begin()
        {

        }

        protected override
            StateMachineStateBase<LaundryStatusFlags, LaundryMachineStateCode, TaskResultCode, TaskResult,
                StateMachineStateType, int, LaundryMachineStateTransition, LaundryVault> PerformGetNextState(
                LaundryMachineStateCode code)
        {
            switch (code)
            {
                case LaundryMachineStateCode.PoweredDown:
                    return new PoweredDownState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness, TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Full:
                    return new FullState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness, TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Error:
                    return new ErrorState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness, TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                default:
                    throw new ArgumentOutOfRangeException(nameof(code), code, null);
            }
        }

        public override void ValidateEntryInvariants()
        {
            using var lck = FlagVault.SpinLock(TimeSpan.FromSeconds(2));
            if (!lck.IsEmpty)
                throw new EntryInvariantsNotMetException(LaundryMachineStateCode.Full,
                    "To be empty, the laundry machine needs to be empty.");
        }

        public override void EstablishExitInvariants()
        {

        }

        protected override LaundryMachineStateTransition[] PerformInitTransitions()
        {
            LaundryMachineStateTransition powerDown = new LaundryMachineStateTransition(this, 0,
                "Empty->Off", "Power down command received.",
                (in LockedLaundryStatus lls) => lls.ShutdownCommandStatus?.StatusCode == CommandRequestStatusCode.Requested,
                (in LockedLaundryStatus lls) => LaundryMachineStateCode.PoweredDown,
                (in LockedLaundryStatus lls, LaundryMachineStateCode? lmsc) =>
                {

                },
                ImmutableArray.Create(LaundryMachineStateCode.PoweredDown, LaundryMachineStateCode.Error));
            LaundryMachineStateTransition full = new LaundryMachineStateTransition(this, 1, "Empty->Full",
                "Laundry loaded.", (in LockedLaundryStatus lls) => !lls.IsEmpty,
                (in LockedLaundryStatus lls) => LaundryMachineStateCode.Full,
                (in LockedLaundryStatus lls, LaundryMachineStateCode? smc) => { },
                ImmutableArray.Create(LaundryMachineStateCode.Full, LaundryMachineStateCode.Error));
            
            return new [] {powerDown, full};
        }


    }
}