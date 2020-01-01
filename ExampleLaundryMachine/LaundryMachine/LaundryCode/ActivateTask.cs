using System;
using JetBrains.Annotations;
using LaundryStatusVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class ActivateTask : LaundryMachineTaskBase
    {
        protected internal ActivateTask([NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext,
            [NotNull] LaundryStatusVault statusFlagsVault, [NotNull] IEventRaiser eventRaiser) : base(executionContext,
            statusFlagsVault, eventRaiser, TaskType.ActivateTask,
            CommandIds.PowerUp) {}

        protected override TaskResult ExecuteTask(CancellationTokenPair pair)
        {
            bool needToClearError;
            using (var lsfLck = LaundryFlags.Lock(TimeSpan.FromSeconds(2)))
            {
                pair.ThrowIfCancellationRequested();
                needToClearError = lsfLck.ExecuteQuery((in LaundryStatusFlags lsf) =>
                    lsf.ErrorRegistrationStatus != ErrorRegistrationStatus.NilStatus);
            }

            if (needToClearError)
            {
                using (var lsfLck = LaundryFlags.Lock(TimeSpan.FromSeconds(2)))
                {
                    lsfLck.ExecuteAction((ref LaundryStatusFlags lsf) =>
                    {
                        bool processing = lsf.ProcessError();
                        if (!processing)
                        {
                            throw new StateLogicErrorException(
                                "Bad state ... error not being handled in correct sequence.");
                        }
                    });
                }

                ClearError(pair);
                pair.ThrowIfCancellationRequested();
                using (var lsfLck = LaundryFlags.Lock(TimeSpan.FromSeconds(2)))
                {
                    lsfLck.ExecuteAction((ref LaundryStatusFlags lsf) =>
                    {
                        bool cleared = lsf.ClearError();
                        if (cleared)
                        {
                            lsf.ResetError();
                        }
                        else
                        {
                            throw new StateLogicErrorException("Ut oh, we couln't fix the error for some reason.");
                        }
                    });
                }
                pair.ThrowIfCancellationRequested();
            }

            TimeSpan simulatedTurnOnCycleTime;
            try
            {
                using var rgen = RandomNumberSource.RGenVault.SpinLock();
                simulatedTurnOnCycleTime = TimeSpan.FromSeconds(rgen.Value.Next(1, 4));
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLineAsync($"Error getting lock on the rgen vault ... exception: [{ex}]");
                simulatedTurnOnCycleTime = TimeSpan.FromSeconds(2);
            }
            
            SimulateWait(in pair, simulatedTurnOnCycleTime);

            TaskResult ret;
            using var lck = TaskResult.SpinLock(TimeSpan.FromSeconds(2));
            ret = lck.Value = lck.Value.WithTerminationTaskResultType(TaskResultCode.SuccessResult);
            return ret;
        }

        private void ClearError(in CancellationTokenPair pair)
        {
            pair.ThrowIfCancellationRequested();
            TimeSpan ts;
            try
            {
                using (var rgen = RandomNumberSource.RGenVault.SpinLock())
                {
                    ts = TimeSpan.FromSeconds(rgen.Value.Next(1, 11));
                }
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLineAsync($"Error getting lock on RGen -- contents: [{ex}]");
                ts = TimeSpan.FromSeconds(3);
            }
            
            SimulateWait(in pair, ts);
            
        }
    }
}