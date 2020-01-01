using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public class DryTask : LaundryMachineTaskBase
    {
        protected override TimeSpan MaxTimeToDispose => TimeSpan.FromSeconds(10);
        protected TimeSpan TimeToDecrementDampnessPerUnit { get; }

        protected internal DryTask([NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext,
            [NotNull] LaundryStatusFlagVault statusFlagsVault, [NotNull] IEventRaiser eventRaiser,
            TimeSpan? timeDecrementOneDampUnit=null) :
            base(executionContext, statusFlagsVault, eventRaiser, TaskType.DryTask, CommandIds.Dry)
        {
            timeDecrementOneDampUnit ??= TimeSpan.FromMilliseconds(150);
            if (timeDecrementOneDampUnit.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeDecrementOneDampUnit), timeDecrementOneDampUnit.Value,
                    @"Value must be positive.");
            TimeToDecrementDampnessPerUnit = timeDecrementOneDampUnit.Value;
        }

        protected sealed override TaskResult ExecuteTask(CancellationTokenPair token)
        {
            Dry(token);
            using var state = TaskResult.SpinLock(TimeSpan.FromMilliseconds(500));
            state.Value = state.Value.WithTerminationTaskResultType(TaskResultCode.SuccessResult);
            return state.Value;
        }

        protected virtual void Dry(CancellationTokenPair pair)
        {
            byte unitsToRemove;
            {
                using var lls = LaundryFlags.SpinLock(TimeSpan.FromSeconds(2));
                unitsToRemove = lls.LoadedLaundryItem != LaundryItems.InvalidItem
                    ? lls.LoadedLaundryItem.Dampness
                    : throw new StateLogicErrorException(
                        "It should not be possible to lack laundry during the wash cycle.");
            }
            byte unitsToDeduct = unitsToRemove;
            TimeSpan timeRequired = TimeToDecrementDampnessPerUnit * unitsToRemove;
            double randomFactor = RandomNumberSource.Next(1, 11) / 100.0; //introduce randomness factor of 10%, either direction
            Debug.Assert(randomFactor >= 0.0 && randomFactor <= 0.11);
            TimeSpan randomTimeToAddOrSub = timeRequired * randomFactor;
            Debug.Assert(randomTimeToAddOrSub <= timeRequired);
            bool negate = RandomNumberSource.Next(1, 3) == 1;

            randomTimeToAddOrSub = negate ? -randomTimeToAddOrSub : +randomTimeToAddOrSub;
            timeRequired += randomTimeToAddOrSub;

            Stopwatch sw = null;
            try
            {
                sw = HighPrecisionTimer;
                sw.Restart();
                SimulateWait(pair, timeRequired);
            }
            catch (OperationCanceledException)
            {
                if (sw == null)
                {
                    string log = "For some reason the stopwatch is null.";
                    TerminationHelper.TerminateApplication(log);
                    return;
                }

                TimeSpan elapsed = sw.Elapsed;
                double percentage = elapsed >= timeRequired ? 1.0 : elapsed / timeRequired;
                unitsToDeduct = Convert.ToByte(Math.Floor(unitsToRemove * percentage));
                throw;
            }
            finally
            {
                sw?.Reset();
                byte newSetting = (byte) (unitsToRemove - unitsToDeduct);
                Debug.Assert(newSetting <= unitsToRemove);
                using var lls = LaundryFlags.SpinLock(TimeSpan.FromSeconds(2));
                lls.SetDampFactor(newSetting);
                Debug.Assert(lls.LoadedLaundryItem.Dampness <= unitsToRemove);
            }




        }


    }
}
