using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    sealed class UnloaderRobot : LaundryRobot
    {
        public static LaundryRobot CreateUnloaderRobot([NotNull] string name,
            [NotNull] [ItemNotNull] IEnumerable<LaundryMachineVault> laundryMachines,
            [NotNull] [ItemNotNull] IEnumerable<ILaundryRepository> laundryRepositories)
        {
            var ret = new UnloaderRobot(name, laundryMachines, laundryRepositories);
            return ret;
        }

        public override LaundryRobotCategory RobotCategory => LaundryRobotCategory.Unloader;

        private UnloaderRobot([NotNull] string name, [NotNull] [ItemNotNull] IEnumerable<LaundryMachineVault> laundryMachines, [NotNull] [ItemNotNull] IEnumerable<ILaundryRepository> laundryRepositories) : base(name, laundryMachines, laundryRepositories)
        {
            var forDirty = Repositories.FirstOrDefault(itm => itm.ForDirtyLaundry);
            var forClean = Repositories.FirstOrDefault(itm => !itm.ForDirtyLaundry);
            _dirtyRepository = forDirty ?? throw new ArgumentException("An unloader robot requires access to a dirty laundry repository.");
            _cleanRepo = forClean ?? throw new ArgumentException("An unloader robot requires access to a clean laundry repository.");
        }

        protected override void ExecuteRobotJob(in CancellationToken token)
        {
            DateTime? ts = null;
            bool doesntHaveLaundry;
            LaundryItems? heldItem;
            string myAction = null;
            try
            {
                using (var lLock = _heldLaundryItem.SpinLock(token))
                {
                    heldItem = lLock.Value;
                    if (heldItem.HasValue)
                    {
                        if (heldItem.Value == LaundryItems.InvalidItem)
                        {
                            TerminationHelper.TerminateApplication(
                                $"Robot {RobotName} has laundry that should not exist.");
                            throw new StateLogicErrorException($"Robot {RobotName} has laundry that should not exist.");
                        }

                        doesntHaveLaundry = false;
                        if (heldItem.Value.SoiledFactor == 0 && heldItem.Value.Dampness == 0)
                        {
                            _cleanRepo.Add(heldItem.Value);
                            ts = TimeStampSource.Now;
                            lLock.Value = null;
                            myAction = "I deposited CLEAN laundry into the clean repository.";
                        }
                        else
                        {
                            _dirtyRepository.Add(heldItem.Value);
                            ts = TimeStampSource.Now;
                            lLock.Value = null;
                            myAction = "I deposited DIRTY OR DAMP laundry into the dirty repository.";
                        }
                    }
                    else
                    {
                        doesntHaveLaundry = true;
                    }
                }

                if (doesntHaveLaundry)
                {
                    TryGetLaundry(token);
                }
                else
                {
                    OnRobotActivity(ts.Value,
                        $"{myAction}  Laundry id: [{heldItem.Value.ItemId.ToString()}], Description: [{heldItem.Value.ItemDescription}]");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            { 
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                TerminationHelper.TerminateApplication(
                    $"Unexpected exception thrown in {RobotName}'s {nameof(ExecuteRobotJob)} method.", ex);
                throw;
            }
        }

        private void TryGetLaundry(in CancellationToken token)
        {
            foreach (var laundryMachineVault in Machines)
            {
                try
                {
                    using var laundryMachine = laundryMachineVault.Lock( token, TimeSpan.FromMilliseconds(250));
                    Debug.WriteLine($"At [{TimeStampSource.Now:O}], Unloader robot [{RobotId}] just got a lock on machine {laundryMachine.MachineId}.");
                    Guid? itemId;
                    string descr;
                    bool? dirty;
                    bool? damp;
                    DateTime? when;
                    long machineId = laundryMachine.MachineId;
                    var res = laundryMachine.UnloadAnyLaundry();
                    if (res.HasValue)
                    {
                        if (res.Value == LaundryItems.InvalidItem)
                        {
                            TerminationHelper.TerminateApplication(
                                $"Robot {RobotName} just retrieved laundry that should not exist from {machineId.ToString()}");
                            throw new StateLogicErrorException(
                                $"Robot {RobotName} just retrieved laundry that should not exist from {machineId.ToString()}");
                        }

                        LaundryItems theItem = res.Value;
                        dirty = theItem.SoiledFactor != 0;
                        damp = theItem.Dampness != 0;
                        itemId = theItem.ItemId;
                        descr = theItem.ItemDescription;

                        try
                        {
                            using (var lck = _heldLaundryItem.SpinLock(token))
                            {
                                if (lck.Value != null)
                                {
                                    TerminationHelper.TerminateApplication(
                                        $"Robot {RobotName} is not supposed to have laundry, but does.");
                                    throw new StateLogicErrorException(
                                        $"Robot {RobotName} is not supposed to have laundry, but does.");
                                }

                                lck.Value = res.Value;
                                when = TimeStampSource.Now;
                                Debug.Assert(lck.Value != null && lck.Value != LaundryItems.InvalidItem);
                            }
                        }
                        catch (TimeoutException ex)
                        {
                            Console.Error.WriteLineAsync(
                                $"Robot timed out when attempting access own held laundry: [{ex}].");
                            TerminationHelper.TerminateApplication(
                                $"Robot timed out when attempting access own held laundry: [{ex}].", ex);
                            return;
                        }

                        OnRobotActivity(when.Value,
                            $"I just unloaded laundry (id: {itemId.ToString()}) (description: {descr}) that is {DampStr(damp)} and {DirtyStr(dirty)} from machine# {machineId.ToString()}.");
                        break;
                    }

                    token.ThrowIfCancellationRequested();
                }
                catch (TimeoutException)
                {
                    Console.Error.WriteLineAsync("Robot timed out while attempting to access machine.");
                }
            }

            static string DampStr(bool? damp) => damp == true ? "damp" : "not damp";
            static string DirtyStr(bool? dirty) => dirty == true ? "dirty" : "clean";
        }


        [NotNull] private readonly ILaundryRepository _dirtyRepository;
        [NotNull] private readonly ILaundryRepository _cleanRepo;



    }

    sealed class LoaderRobot : LaundryRobot
    {
        public static LaundryRobot CreateLoaderRobot([NotNull] string name,
            [NotNull] [ItemNotNull] IEnumerable<LaundryMachineVault> laundryMachines,
            [NotNull] [ItemNotNull] IEnumerable<ILaundryRepository> laundryRepositories)
        {
            var ret = new LoaderRobot(name, laundryMachines, laundryRepositories);
            return ret;
        }

        public override LaundryRobotCategory RobotCategory => LaundryRobotCategory.Loader;
        protected override void ExecuteRobotJob(in CancellationToken token)
        {
            {
                bool doesntHaveLaundry;
                (bool SuccessfullyLoaded, bool SuccessfullyCycled, DateTime? LoadedTime, Guid LaundryId, long? MachineId)? res = null;
                try
                {
                    //check if has laundry
                    using var lLock = _heldLaundryItem.SpinLock(token);
                    if (lLock.Value == null)
                    {
                        doesntHaveLaundry = true;
                    }
                    else
                    {
                        res = TryLoadLaundryItem(in lLock, token);
                        doesntHaveLaundry = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (TimeoutException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    TerminationHelper.TerminateApplication(
                        $"Unexpected exception thrown in {RobotName}'s {nameof(ExecuteRobotJob)} method.", ex);
                    throw;
                }
                
                if (doesntHaveLaundry)
                {
                    TryGetLaundryItem(token);
                    token.ThrowIfCancellationRequested();
                }
                else
                {
                    var realRes = res.Value;
                    if (realRes.SuccessfullyLoaded)
                    {
                        
                        string whatIDidString = realRes.SuccessfullyCycled
                            ? "I loaded and started cycle"
                            : "I loaded but could not start cycle";
                        string actionString =
                            $"{whatIDidString} for laundry with Id [{realRes.LaundryId.ToString()}] into machine# {realRes.MachineId?.ToString() ?? "UNKNOWN"}";
                        OnRobotActivity(realRes.LoadedTime.Value, actionString);
                    }
                    token.ThrowIfCancellationRequested();
                }
            }
        }

        private void TryGetLaundryItem(in CancellationToken token)
        {
            var getRes = _dirtyRepository.Remove(TimeSpan.FromMilliseconds(100), token);
            if (getRes.RemovedOk)
            {
                var ts = TimeStampSource.Now;
                LaundryItems item = getRes.Item;
                if (item == LaundryItems.InvalidItem)
                {
                    TerminationHelper.TerminateApplication($"Robot {RobotName} just pulled non-existent laundry out of the dirty bin.");
                    return;
                }

                using (var lck = _heldLaundryItem.SpinLock(token))
                {
                    if (lck.Value != null)
                    {
                        TerminationHelper.TerminateApplication($"Robot {RobotName} just retrieved dirty laundry even though he already has some.");
                        throw new StateLogicErrorException($"Robot {RobotName} just retrieved dirty laundry even though he already has some.");
                    }
                    lck.Value = item;
                }

                OnRobotActivity(ts,
                    $"I just retrieved laundry from the dirty bin with Id: [{item.ItemId}] and description: [{item.ItemDescription}]");
            }
            token.ThrowIfCancellationRequested();
        }

        private (bool SuccessfullyLoaded, bool SuccessfullyCycled, DateTime? LoadedTime, Guid LaundryId, long? MachineId) TryLoadLaundryItem(in LockedVaultObject<BasicVault<LaundryItems?>, LaundryItems?> lLock, in CancellationToken token)
        {
            DateTime? loadedTime = null;
            bool successfullyLoaded=false;
            bool successfullyCycled=false;
            long? chosenMachineId = null;
            var value = lLock.Value ?? LaundryItems.InvalidItem;
            if (value == LaundryItems.InvalidItem)
            {
                TerminationHelper.TerminateApplication($"Robot name {RobotName} is trying to load non-existent laundry!");
            }
            foreach (var machineVault in Machines)
            {
                try
                {
                    using var machine = machineVault.Lock(token, TimeSpan.FromMilliseconds(250));
                    Debug.WriteLine($"Loader just got a lock on machine id [{machine.MachineId}]");
                    if (machine.StateCode == LaundryMachineStateCode.Empty)
                    {
                        //.Where(machine => machine.StateCode == LaundryMachineStateCode.Empty)
                        try
                        {
                            var ret = machine.LoadAndCycle(value);
                            successfullyLoaded = ret.LoadedLaundry != null;
                            successfullyCycled = ret.Cycled;
                            if (successfullyLoaded)
                            {
                                loadedTime = TimeStampSource.Now;
                                chosenMachineId = machine.MachineId;
                                lLock.Value = null;
                                break;
                            }

                            token.ThrowIfCancellationRequested();
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (TimeoutException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLineAsync(ex.ToString());
                            throw;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Loader robot will ingote machine --- it isn't empty.");
                    }
                }
                
                catch (TimeoutException)
                {
                    Console.Error.WriteLineAsync(
                        $"Robot in category {RobotCategory.ToString()} with id {RobotId.ToString()} timed out during attempt to access laundry machine.");
                }
            }

            return (successfullyLoaded, successfullyCycled, loadedTime, value.ItemId, chosenMachineId);
        }

        private LoaderRobot([NotNull] string name, [NotNull] [ItemNotNull] IEnumerable<LaundryMachineVault> laundryMachines,
            [NotNull] [ItemNotNull] IEnumerable<ILaundryRepository> laundryRepositories) : base(name, laundryMachines,
            laundryRepositories)
        {
            var forDirty = Repositories.FirstOrDefault(itm => itm.ForDirtyLaundry);
            _dirtyRepository =
                forDirty ?? throw new ArgumentException(
                    "A loader robot requires access to a dirty laundry repository.");
        }

        [NotNull] private readonly ILaundryRepository _dirtyRepository;
    }
}
