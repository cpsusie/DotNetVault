using System;
using System.Diagnostics;
using JetBrains.Annotations;
using LockedLaundryStatus =LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class WashTask : LaundryMachineTaskBase
    {
        protected override TimeSpan MaxTimeToDispose => TimeSpan.FromSeconds(10);
        protected TimeSpan TimeToIncrementDampnessPerUnit { get; }
        protected TimeSpan TimeToRemoveOneUnitOfSoil { get; }
    
        protected internal WashTask([NotNull] ILaundryMachineTaskExecutionContext<TaskResult> executionContext,
            [NotNull] LaundryStatusFlagVault statusFlagsVault, [NotNull] IEventRaiser eventRaiser, TimeSpan? timeIncreaseDampnessPerUnit = null, TimeSpan? timeRemoveOneUnitSoil = null) :
            base(executionContext, statusFlagsVault, eventRaiser, TaskType.WashTask, CommandIds.Wash)
        {
            timeIncreaseDampnessPerUnit ??= TimeSpan.FromMilliseconds(50);
            timeRemoveOneUnitSoil ??= TimeSpan.FromMilliseconds(100);

            if (timeIncreaseDampnessPerUnit <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeIncreaseDampnessPerUnit), timeIncreaseDampnessPerUnit,
                    @"Value must be positive.");
            if (timeRemoveOneUnitSoil <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeRemoveOneUnitSoil), timeRemoveOneUnitSoil,
                    @"Value must be positive.");
            TimeToIncrementDampnessPerUnit = timeIncreaseDampnessPerUnit.Value;
            TimeToRemoveOneUnitOfSoil = timeRemoveOneUnitSoil.Value;
        }

        protected sealed override TaskResult ExecuteTask(CancellationTokenPair token)
        {
            Soak(token);
            token.ThrowIfCancellationRequested();
            Cleanse(token);
            using var state = TaskResult.SpinLock(TimeSpan.FromMilliseconds(500));
            state.Value = state.Value.WithTerminationTaskResultType(TaskResultCode.SuccessResult);
            return state.Value;
        }

        protected virtual void Cleanse(CancellationTokenPair token)
        {
            byte unitsToRemove;
            {
                using var lls = LaundryFlags.SpinLock(TimeSpan.FromSeconds(2));
                unitsToRemove = lls.LoadedLaundryItem != LaundryItems.InvalidItem
                    ? lls.LoadedLaundryItem.SoiledFactor
                    : throw new StateLogicErrorException(
                        "It should not be possible to lack laundry during the wash cycle.");
            }
            byte unitsToDeduct = unitsToRemove;
            TimeSpan timeRequired = TimeToRemoveOneUnitOfSoil * unitsToRemove;
            Stopwatch sw = null;
            try
            {
                sw = HighPrecisionTimer;
                sw.Restart();
                if (timeRequired > TimeSpan.Zero)
                {
                    SimulateWait(token, timeRequired, "Beginning cleanse wait.");
                }

            }
            catch (IndividualOperationCancelledException)
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
                lls.SetSoilFactor(newSetting);
                Debug.Assert(lls.LoadedLaundryItem.SoiledFactor <= unitsToRemove);
            }
        }

        protected virtual void Soak(CancellationTokenPair token)
        {
            Stopwatch sw = null;
            try
            {
                Debug.WriteLine("Beginning Cleanse Soak");
                byte oldDampness;
                byte newDampness;
                sw = HighPrecisionTimer;
                sw.Reset();
                sw.Start();
                {
                    using LockedLaundryStatus lls =
                        LaundryFlags.SpinLock(token.IndividualToken, TimeSpan.FromSeconds(2));
                    var res = lls.SoakLaundry() ??
                              throw new StateLogicErrorException(
                                  "It is supposed to be impossible to start the machine without laundry in it.");
                    oldDampness = res.OldDampness;
                    newDampness = res.NewDampness;
                }

                Debug.Assert(newDampness >= oldDampness);
                int dampnessUnitsIncrease = newDampness - oldDampness;
                TimeSpan totalTimeRequired = TimeToIncrementDampnessPerUnit * dampnessUnitsIncrease;
                TimeSpan timeRemaining = totalTimeRequired - sw.Elapsed;
                if (timeRemaining > TimeSpan.Zero)
                {
                    SimulateWait(token, timeRemaining, "Beginning soak wait", "Ending soak wait");
                }
            }
            catch (StateLogicErrorException ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                Environment.Exit(-1);
            }
            catch (TimeoutException)
            {
                Console.Error.WriteAsync(
                    $"Unable to obtain lock in {nameof(Soak)} method of {nameof(WashTask)} task.");
                throw;
            }
            finally
            {
                sw?.Reset();
                Debug.WriteLine("Ending soak.");
            }
        }
    }

    
}
