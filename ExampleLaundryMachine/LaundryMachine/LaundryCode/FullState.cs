using System;
using System.Collections.Immutable;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class FullState : LaundryStateMachineState
    {
        public FullState([NotNull] LaundryVault vault,
            [NotNull] BasicVault<LaundryMachineStateCode> stateCodeVault, [NotNull] IEventRaiser raiser,
            [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext, TimeSpan addOneUnitDamp,
            TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            StateMachineStateType.WaitForMoreInputOnly, LaundryMachineStateCode.Full, vault, stateCodeVault, raiser,
            executionContext, addOneUnitDamp, removeOneUnitDirt, removeOneUnitDamp) {}

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
                    return new PoweredDownState(FlagVault, _stateCodeVault, EventRaiser, _context,
                        TimeToAddOneUnitDampness, TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Empty:
                    return new EmptyState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Washing:
                    return new WashState(EventRaiser, _stateCodeVault, FlagVault, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Drying:
                    return new DryState(EventRaiser, _stateCodeVault, FlagVault, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                default:
                case LaundryMachineStateCode.Error:
                    return new ErrorState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
            }
        }

        public override void ValidateEntryInvariants()
        {
            using var lck = FlagVault.SpinLock(TimeSpan.FromSeconds(2));
            if (lck.IsEmpty) throw new EntryInvariantsNotMetException(LaundryMachineStateCode.Full, "To be full, the laundry machine needs to have laundry in it.");
        }

        public override void EstablishExitInvariants()
        {
            
        }

        protected override LaundryMachineStateTransition[] PerformInitTransitions()
        {
            LaundryMachineStateTransition powerDown = new LaundryMachineStateTransition(this, 0,
                "Full->Off", "Power down command received.",
                (in LockedLaundryStatus lls) => lls.ShutdownCommandStatus?.StatusCode == CommandRequestStatusCode.Requested,
                (in LockedLaundryStatus lls) => LaundryMachineStateCode.PoweredDown,
                (in LockedLaundryStatus lls, LaundryMachineStateCode? lmsc) =>
                {

                },
                ImmutableArray.Create(LaundryMachineStateCode.PoweredDown, LaundryMachineStateCode.Error));
            LaundryMachineStateTransition empty = new LaundryMachineStateTransition(this, 1, "Full->Empty",
                "Laundry removed.", (in LockedLaundryStatus lls) => lls.IsEmpty,
                (in LockedLaundryStatus lls) => LaundryMachineStateCode.Empty,
                (in LockedLaundryStatus lls, LaundryMachineStateCode? smc) => { },
                ImmutableArray.Create(LaundryMachineStateCode.Empty, LaundryMachineStateCode.Error));
            LaundryMachineStateTransition wash = new LaundryMachineStateTransition(this, 2, "Full->Washing",
                "Received Wash Command",
                (in LockedLaundryStatus lls) =>
                    lls.WashCommandRequestStatus?.StatusCode == CommandRequestStatusCode.Requested,
                (in LockedLaundryStatus lls) => LaundryMachineStateCode.Washing,
                (in LockedLaundryStatus lls, LaundryMachineStateCode? code) => { },
                ImmutableArray.Create(LaundryMachineStateCode.Washing, LaundryMachineStateCode.Error));
            LaundryMachineStateTransition dry = new LaundryMachineStateTransition(this, 3, "Full->Drying",
                "Received dry command, not wash command",
                (in LockedLaundryStatus lls) => lls.WashCommandRequestStatus?.StatusCode != CommandRequestStatusCode.Requested &&
                                                lls.DryCommandRequestStatus?.StatusCode == CommandRequestStatusCode.Requested,
                (in LockedLaundryStatus lls) => LaundryMachineStateCode.Drying,
                (in LockedLaundryStatus lls, LaundryMachineStateCode? code) => { },
                ImmutableArray.Create(LaundryMachineStateCode.Drying, LaundryMachineStateCode.Error));
            return new[]
            {
                powerDown, empty, wash, dry
            };
        }                          


    }
}