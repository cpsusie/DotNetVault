using System;
using System.Collections.Immutable;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class WashState : LaundryMachineTaskBasedStateBase<WashTask>
    {
        protected internal WashState([NotNull] IEventRaiser raiser,
            [NotNull] BasicVault<LaundryMachineStateCode> stateVault, [NotNull] LaundryVault vault,
            [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext, TimeSpan addOneUnitDamp,
            TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            LaundryMachineStateCode.Washing,
            CommandIds.Wash, raiser, stateVault, vault, executionContext,
            ImmutableArray.Create(LaundryMachineStateCode.Full, LaundryMachineStateCode.Drying), addOneUnitDamp,
            removeOneUnitDirt, removeOneUnitDamp) {}

        protected override string EntryInvariantDescription => "The wash command must be in the requested state.";

        protected override string ExitInvariantDescription =>
            "The wash command must be cleared and any cancellation status reset.  On cancellation: 1- any Dry command must be cleared. 2- any cancellation status must be reset 3- clear current command.";
        protected sealed override LTransProcedure InitTaskEndedTransProcedure() => NextStateWhenEnded;

        protected sealed override LTransAdditionalProcedure InitTaskEndedAdditionalTransProc() =>
            PerformAdditionalNextStateWhenEndedActions;
        protected virtual LaundryMachineStateCode? NextStateWhenEnded(in LockedLaundryStatus lls)
        {
            var res = lls.FindCommandById(CommandId);
            if (!res.HasValue) return LaundryMachineStateCode.Error;
            LaundryMachineStateCode? ret;
            switch (res.Value.StatusCode)
            {
                default:
                case CommandRequestStatusCode.Faulted:
                    ret = LaundryMachineStateCode.Error;
                    break;
                case CommandRequestStatusCode.Cancelled:
                    ret = LaundryMachineStateCode.Full;
                    break;
                case CommandRequestStatusCode.Completed:
                    ret = lls.DryCommandRequestStatus?.StatusCode == CommandRequestStatusCode.Requested
                        ? LaundryMachineStateCode.Drying
                        : LaundryMachineStateCode.Full;
                    break;
            }
            return ret;

        }

        protected virtual void PerformAdditionalNextStateWhenEndedActions(in LockedLaundryStatus lls,
            LaundryMachineStateCode? code)
        {
            if (code == LaundryMachineStateCode.Full &&
                lls.DryCommandRequestStatus?.StatusCode != CommandRequestStatusCode.Nil)
            {
                lls.ForceClearDryCommandAndAnyCurrentCommand();
            }
        }

        protected override WashTask CreateLaundryTask() => new WashTask(_context, FlagVault, EventRaiser);
        protected override void ValidateOtherEntryInvariants(in LockedLaundryStatus lls) => lls.SetCurrentCommandToSpecified(CommandId);

        protected override
            StateMachineStateBase<LaundryStatusFlags, LaundryMachineStateCode, TaskResultCode, TaskResult,
                StateMachineStateType, int, LaundryMachineStateTransition, LaundryVault> PerformGetNextState(
                LaundryMachineStateCode code)
        {
            switch (code)
            {
                default:
                case LaundryMachineStateCode.Error:
                    return new ErrorState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Full:
                    return new FullState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Drying:
                    return new DryState(EventRaiser, _stateCodeVault, FlagVault, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
            }
        }
    }
}
