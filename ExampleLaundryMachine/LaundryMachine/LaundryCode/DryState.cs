using System;
using System.Collections.Immutable;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class DryState : LaundryMachineTaskBasedStateBase<DryTask>
    {
        protected internal DryState([NotNull] IEventRaiser raiser,
            [NotNull] BasicVault<LaundryMachineStateCode> stateVault, [NotNull] LaundryVault vault,
            [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext, TimeSpan addOneUnitDamp,
            TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            LaundryMachineStateCode.Drying,
            CommandIds.Dry, raiser, stateVault, vault, executionContext,
            ImmutableArray.Create(LaundryMachineStateCode.Full, LaundryMachineStateCode.Error), addOneUnitDamp,
            removeOneUnitDirt, removeOneUnitDamp) {}

        protected override string EntryInvariantDescription => "The dry command must be in the requested state.";

        protected override string ExitInvariantDescription =>
            "The dry command must be cleared and any cancellation status reset.  There must be no current command.";

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
                case CommandRequestStatusCode.Completed:
                case CommandRequestStatusCode.Cancelled:
                    ret = LaundryMachineStateCode.Full;
                    break;
            }
            return ret;
        }
        protected override void ValidateOtherEntryInvariants(in LockedLaundryStatus lls) => lls.SetCurrentCommandToSpecified(CommandId);
        protected virtual void PerformAdditionalNextStateWhenEndedActions(in LockedLaundryStatus lls,
            LaundryMachineStateCode? code)
        {
            
        }

        protected override DryTask CreateLaundryTask() => new DryTask(_context, FlagVault, EventRaiser);

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
            }
        }

    }
}
