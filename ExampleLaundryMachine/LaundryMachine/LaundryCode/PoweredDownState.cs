using System;
using System.Collections.Immutable;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public sealed class PoweredDownState : LaundryStateMachineState
    {

        internal PoweredDownState([NotNull] LaundryVault vault,
            [NotNull] BasicVault<LaundryMachineStateCode> stateVault, [NotNull] IEventRaiser raiser,
            [NotNull] ILaundryMachineTaskExecutionContext<TaskResult> context, TimeSpan addOneUnitDamp,
            TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp) : base(
            StateMachineStateType.WaitForMoreInputOnly,
            LaundryMachineStateCode.PoweredDown, vault, stateVault, raiser, context, addOneUnitDamp, removeOneUnitDirt, removeOneUnitDamp)
        {
            if (!context.IsActive || context.IsDisposed || context.IsFaulted)
                throw new ArgumentException("Context must not be in a disposed or a faulted state and must be active.");
        }


        public override void Begin()
        {
            
        }


        protected override
            StateMachineStateBase<LaundryStatusFlags, LaundryMachineStateCode, TaskResultCode, TaskResult,
                StateMachineStateType, int, LaundryMachineStateTransition, LaundryVault> PerformGetNextState(
                LaundryMachineStateCode code)
        {
            if (code == LaundryMachineStateCode.Activating)
                return new ActivatingState(EventRaiser, _stateCodeVault, FlagVault, _context, TimeToAddOneUnitDampness,
                    TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
            if (code == LaundryMachineStateCode.PoweredDown)
                return new PoweredDownState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                    TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
            return new ErrorState(FlagVault, _stateCodeVault, EventRaiser, _context, TimeToAddOneUnitDampness,
                TimeToRemoveOneUnitDirt, TimeToRemoveOneUnitDampness);
        }

        public override void ValidateEntryInvariants()
        {
            try
            {
                using var lck = FlagVault.SpinLock(TimeSpan.FromSeconds(2));
                if (lck.ShutdownCommandStatus?.StatusCode != CommandRequestStatusCode.Nil)
                {
                    //bug 61 fix -- uncommented next line now rightly causes compilation error.
                    //lck.Dispose();
                    lck.ForceClearPowerDownStatus();
                }
            }
            catch (EntryInvariantsNotMetException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                OnTimedOutGettingStatusLock();
            }
            catch (Exception e)
            {
                OnUnexpectedExceptionThrown(e);
            }
        }

        public override void EstablishExitInvariants()
        {
            try
            {
                //no exit invariants
            }
            catch (TimeoutException)
            {
                OnTimedOutGettingStatusLock();
            }
            catch (Exception e)
            {
                OnUnexpectedExceptionThrown(e);
            }
        }


        protected  override LaundryMachineStateTransition[] PerformInitTransitions()
        {

            LaundryMachineStateTransition transition = new LaundryMachineStateTransition(this, 0, "Off->PowerUp",
                "Activation Requested",
                (in LockedLaundryStatus lls) =>
                    lls.ActivationCommandStatus?.StatusCode == CommandRequestStatusCode.Requested,
                (in LockedLaundryStatus lls) => LaundryMachineStateCode.Activating,
                (in LockedLaundryStatus lls, LaundryMachineStateCode? c) => { },
                ImmutableArray.Create(LaundryMachineStateCode.Activating));
            return new [] {transition};
        }
        
    }
}