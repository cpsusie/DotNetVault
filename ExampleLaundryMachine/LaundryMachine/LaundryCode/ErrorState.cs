using System;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class ErrorState : LaundryStateMachineState
    {
        public ErrorState([NotNull] LaundryVault statusVault,
            BasicVault<LaundryMachineStateCode> basicV,
            IEventRaiser eventRaiser, [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> context,
            TimeSpan addOneUnitDamp, TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            StateMachineStateType.Error, LaundryMachineStateCode.Error, statusVault, basicV, eventRaiser, context,
            addOneUnitDamp, removeOneUnitDirt, removeOneUnitDamp) =>
            throw new NotImplementedException();

        public override void Begin()
        {
            throw new NotImplementedException();
        }


        protected override StateMachineStateBase<LaundryStatusFlags, LaundryMachineStateCode, TaskResultCode, TaskResult, StateMachineStateType, int, LaundryMachineStateTransition, LaundryVault> PerformGetNextState(LaundryMachineStateCode code) => throw new NotImplementedException();

        public override void ValidateEntryInvariants()
        {
            throw new NotImplementedException();
        }

        public override void EstablishExitInvariants()
        {
            throw new NotImplementedException();
        }

        protected override LaundryMachineStateTransition[] PerformInitTransitions() => throw new NotImplementedException();
    }
}