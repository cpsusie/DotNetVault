using System;
using System.Collections.Immutable;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class ActivatingState : LaundryMachineTaskBasedStateBase<ActivateTask>
    {
        protected internal ActivatingState([NotNull] IEventRaiser raiser,
            [NotNull] BasicVault<LaundryMachineStateCode> stateVault, [NotNull] LaundryVault vault,
            [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext, TimeSpan addOneUnitDamp,
            TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            LaundryMachineStateCode.Activating, CommandIds.PowerUp, raiser, stateVault,
            vault, executionContext,
            ImmutableArray.Create(LaundryMachineStateCode.Empty, LaundryMachineStateCode.Full,
                LaundryMachineStateCode.PoweredDown), addOneUnitDamp, removeOneUnitDirt, removeOneUnitDamp) {}

        protected override
            StateMachineStateBase<LaundryStatusFlags, LaundryMachineStateCode, TaskResultCode, TaskResult,
                StateMachineStateType, int, LaundryMachineStateTransition, LaundryVault> PerformGetNextState(
                LaundryMachineStateCode code)
        {
            switch (code)
            {
                default:
                case LaundryMachineStateCode.Empty:
                    return new EmptyState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Error:
                    return new ErrorState(FlagVault, _stateCodeVault, EventRaiser,
                        _context, TimeToAddOneUnitDampness, TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
                case LaundryMachineStateCode.Full:
                    return new FullState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                        TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
            }
        }

        protected sealed override LTransProcedure InitTaskEndedTransProcedure() => NextStateWhenEnded;

        protected sealed override LTransAdditionalProcedure InitTaskEndedAdditionalTransProc() =>
            PerformAdditionalNextStateWhenEndedActions;

        protected override string EntryInvariantDescription => "The power up command must be registered.";

        protected virtual LaundryMachineStateCode? NextStateWhenEnded(in LockedLaundryStatus lls)
        {
            var res = lls.FindCommandById(CommandId);
            if (!res.HasValue) return LaundryMachineStateCode.Error;
            LaundryMachineStateCode? ret;
            switch (res.Value.StatusCode)
            {
                default:
                    ret = LaundryMachineStateCode.Error;
                    break;
                case CommandRequestStatusCode.Completed:
                    ret = lls.IsEmpty ? LaundryMachineStateCode.Empty : LaundryMachineStateCode.Full;
                    break;
                case CommandRequestStatusCode.Faulted:
                    ret = LaundryMachineStateCode.Error;
                    break;
                case CommandRequestStatusCode.Cancelled:
                    ret = LaundryMachineStateCode.PoweredDown;
                    break;
            }
            return ret;
        }

        protected virtual void PerformAdditionalNextStateWhenEndedActions(in LockedLaundryStatus lls, LaundryMachineStateCode? code) { }

        protected override string ExitInvariantDescription =>
            "The power up command must be cleared and any cancellation status reset.";

        protected override ActivateTask CreateLaundryTask() => new ActivateTask(_context, FlagVault, EventRaiser);
    }
}