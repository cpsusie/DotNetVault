using System;
using System.Diagnostics;
using DotNetVault.Attributes;
using JetBrains.Annotations;
using CommandIdBacker = System.Int32;
using CommandRequestStatusBacker = System.Int32;
using LockedStateCode = DotNetVault.LockedResources.LockedVaultObject<DotNetVault.Vaults.
    BasicVault<LaundryMachine.LaundryCode.LaundryMachineStateCode>, 
    LaundryMachine.LaundryCode.LaundryMachineStateCode>;
using ErrorRegistrationStatusBacker = System.Int32;
namespace LaundryMachine.LaundryCode
{
    internal interface IFindCommandRequestStatusRef
    {
        ref CommandRequestStatus FindCommandRequestStatusById(CommandIds ids);

    }
    public sealed class LaundryStatusFlags : IFindCommandRequestStatusRef
    {
        [NotNull]
        public string LoadedLaundryDescription => string.IsNullOrEmpty(_loadedItem.ItemDescription)
            ? "NO LOADED LAUNDRY"
            : _loadedItem.ItemDescription;
        public bool IsEmpty => _loadedItem == LaundryItems.InvalidItem;
        public Guid? LoadedLaundryId => _loadedItem != LaundryItems.InvalidItem ? (Guid?) _loadedItem.ItemId : null;
        public ref readonly LaundryItems LoadedLaundry => ref _loadedItem;
        public ref readonly CancelFlag CancelFlag => ref _cancelFlag;
        public CommandRequestStatus? CurrentCommand 
        {
            get
            {
                switch (_currentCommand)
                {
                    case CommandIds.PowerUp:
                        return _activationCommandStatus;
                    case CommandIds.Wash:
                        return _washCommandStatus;
                    case CommandIds.Dry:
                        return _dryCommandStatus;
                    default:
                    case null:
                        return null;
                        
                    
                }
            }
        }
        public ref readonly CommandRequestStatus DryCommandStatus => ref _dryCommandStatus;
        public ref readonly CommandRequestStatus ActivationCommandStatus => ref _activationCommandStatus;
        public ref readonly CommandRequestStatus ShutdownCommandStatus => ref _shutdownCommandRequestStatus;
        public ref readonly CommandRequestStatus WashCommandRequestStatus => ref _washCommandStatus;
        public ref readonly ErrorRegistrationStatus ErrorRegistrationStatus =>
            ref _errorRegistrationStatus;
        internal ref LaundryItems RefToLaundry => ref _loadedItem;
        internal ref CommandIds? RefToCurrentCommand => ref _currentCommand;
        public CommandRequestStatus? FindCommandById(in CommandIds? commandIds)
        {
            switch (commandIds)
            {
                case CommandIds.Dry:
                    return DryCommandStatus;
                case CommandIds.PowerUp:
                    return ActivationCommandStatus;
                case CommandIds.Shutdown:
                    return ShutdownCommandStatus;
                case CommandIds.Wash:
                    return WashCommandRequestStatus;
               case CommandIds.StartPostErrorDiagnostic:
                    return ActivationCommandStatus;
                case null:
                default:
                    return null;
            }
        }

        ref CommandRequestStatus IFindCommandRequestStatusRef.FindCommandRequestStatusById(CommandIds ids)
        {
            switch (ids)
            {
                case CommandIds.Dry:
                    return ref _dryCommandStatus;
                case CommandIds.PowerUp:
                    return ref _activationCommandStatus;
                case CommandIds.Shutdown:
                    return ref _shutdownCommandRequestStatus;
               case CommandIds.Wash:
                    return ref _washCommandStatus;
               case CommandIds.StartPostErrorDiagnostic:
                    return ref _activationCommandStatus;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ids), ids, @"Enum not value not defined.");
            }
        }

        public Guid? LoadLaundry(in LaundryItems laundry)
        {
            if (IsEmpty && laundry != LaundryItems.InvalidItem)
            {
                _loadedItem = laundry;
                return _loadedItem.ItemId;
            }
            return null;
        }

        public bool SetSoiledFactor(byte newFactor)
        {
            if (IsEmpty) return false;
            _loadedItem = _loadedItem.WithSoilFactor(newFactor);
            return _loadedItem.SoiledFactor == newFactor;
        }

        public bool SetDampnessFactor(byte dampness)
        {
            if (IsEmpty) return false;
            _loadedItem = _loadedItem.WithDampness(dampness);
            return _loadedItem.Dampness == dampness;
        }

        public (byte OldDampness, byte NewDampness)? SoakLoadedLaundry()
        {
            if (IsEmpty) return null;
            
            ref LaundryItems item = ref _loadedItem;
            byte old = item.Dampness;

            if (old < 255u)
                item = item.WithDampness((byte) 255u);
            return (old, item.Dampness);
        }

        public LaundryItems? UnloadLaundry(in Guid id)
        {
            LaundryItems? ret = null;
            if (_loadedItem.ItemId == id)
            {
                ret = _loadedItem;
                _loadedItem = LaundryItems.InvalidItem;
            }
            return ret;
        }

        public LaundryItems? UnloadAnyLaundry()
        {
            LaundryItems? ret = null;
            if (_loadedItem != LaundryItems.InvalidItem)
            {
                ret = _loadedItem;
                _loadedItem = LaundryItems.InvalidItem;
            }
            Debug.Assert(ret != LaundryItems.InvalidItem);
            return ret;
        }

        public LaundryItems? UnloadAnyCleanAndDryLaundry()
        {
            LaundryItems? ret = null;
            if (_loadedItem != LaundryItems.InvalidItem && _loadedItem.SoiledFactor == 0 && _loadedItem.Dampness == 0)
            {
                ret = _loadedItem;
                _loadedItem = LaundryItems.InvalidItem;
            }
            Debug.Assert(ret != LaundryItems.InvalidItem);
            return ret;
        }

        public bool RegisterCancelCurrentTask()
        {
            var temp = _cancelFlag.TryRegisterRequest();
            if (temp.HasValue)
            {
                _cancelFlag = temp.Value;
                return true;
            }
            return false;
        }

        public bool RegisterCancellationPending()
        {
            var temp = _cancelFlag.TryRegisterPending();
            if (temp.HasValue)
            {
                _cancelFlag = temp.Value;
                return true;
            }

            return false;
        }

        public bool RegisterCancellationComplete()
        {
            var temp = _cancelFlag.TryRegisterComplete();
            if (temp.HasValue)
            {
                _cancelFlag = temp.Value;
                return true;
            }

            return false;
        }

        public bool ResetCancellation()
        {
            CancelFlag current = _cancelFlag;
            _cancelFlag = _cancelFlag.Reset();
            return current.State != CancelState.Nil;
        }


        public bool RegisterPowerOnCommand(LaundryMachineStateCode stateCode)
        {
            try
            {
                if (_currentCommand != null)
                {
                    return false;
                }

                DateTime reqTime = TimeStampSource.Now;
                if (stateCode != LaundryMachineStateCode.PoweredDown ||
                    _errorRegistrationStatus != ErrorRegistrationStatus.NilStatus)
                {
                    return false;
                }

                _activationCommandStatus = _activationCommandStatus.AsRequested(reqTime);
                _currentCommand = _activationCommandStatus.CommandId;
                Debug.Assert(_activationCommandStatus.StatusCode == CommandRequestStatusCode.Requested);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                return false;
            }
        }

        public bool AcknowledgePowerUpPending(LaundryMachineStateCode stateCode)
        {
            try
            {
                Debug.Assert(_currentCommand == _activationCommandStatus.CommandId);
                DateTime ackTime = TimeStampSource.Now;
                if (stateCode != LaundryMachineStateCode.PoweredDown ||
                    stateCode != LaundryMachineStateCode.Error)
                    return false;
                _activationCommandStatus = _activationCommandStatus.AsPending(ackTime);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                return false;
            }
        }

        public bool SignalPowerUpRefused()
        {
            try
            {
                Debug.Assert(_currentCommand== _activationCommandStatus.CommandId);
                DateTime refusedTime = TimeStampSource.Now;
                _activationCommandStatus = _activationCommandStatus.AsRejected(refusedTime);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                return false;
            }
        }

        public bool SignalPowerUpComplete(DateTime? ts = null, bool andReset = true)
        {
            try
            {
                Debug.Assert(_currentCommand == _activationCommandStatus.CommandId);
                DateTime completeAt = ts ?? TimeStampSource.Now;
                _activationCommandStatus = _activationCommandStatus.AsRanToCompletion(completeAt);
                if (andReset)
                {
                    _activationCommandStatus = _activationCommandStatus.AsReset();
                    _currentCommand = null;
                }

                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                return false;
            }
        }

        public bool SignalPowerUpCancelled()
        {
            try
            {
                Debug.Assert(_currentCommand == _activationCommandStatus.CommandId);
                _activationCommandStatus =
                    _activationCommandStatus.AsCancelled(TimeStampSource.Now, "User requested cancellation.");
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                return false;
            }
        }

        public bool SignalPowerUpFaulted([NotNull] string explanation, [CanBeNull] Exception error)
        {
            try
            {
                Debug.Assert(_currentCommand == _activationCommandStatus.CommandId);
                DateTime faultTime = TimeStampSource.Now;
                _activationCommandStatus = error != null
                    ? _activationCommandStatus.AsFaultedBcException(faultTime, explanation,
                        error)
                    : _activationCommandStatus.AsFaultedNoException(faultTime, explanation);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return false;
            }
        }

        public bool SignalPowerUpReset()
        {
            try
            {
                _activationCommandStatus = _activationCommandStatus.AsReset();
                if (_currentCommand == _activationCommandStatus.CommandId)
                {
                    _currentCommand = null;
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public bool RegisterError(string explanation, bool isLogicError)
        {
            try
            {
                _errorRegistrationStatus =
                    _errorRegistrationStatus.AsRegistered(TimeStampSource.Now, explanation ?? "An unknown error occured.",
                        isLogicError);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                Debug.WriteLine(ex.ToString());
                return false;
            }
        }

        public bool ProcessError()
        {
            try
            {
                if (_errorRegistrationStatus.IsLogicError)
                {
                    Console.Error.WriteLineAsync(
                        "A logic error is not recoverable -- it is a program bug, not a simulated fault in the laundry machine.");
                    return false;
                }

                if (_errorRegistrationStatus.StatusCode == ErrorRegistrationStatusCode.Registered)
                {
                    _errorRegistrationStatus = _errorRegistrationStatus.AsProcessed(TimeStampSource.Now,
                        "Going to try to recover from error by hammering it until it works.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                return false;
            }

            return false;
        }

        public bool ClearCurrentCommandIfMatch(CommandIds id)
        {
            if (_currentCommand == id)
            {
                _currentCommand = null;
                return true;
            }
            return false;
        }

        public bool ClearError()
        {
            try
            {
                if (_errorRegistrationStatus.StatusCode == ErrorRegistrationStatusCode.Processed)
                {
                    _errorRegistrationStatus =
                        _errorRegistrationStatus.AsCleared(TimeStampSource.Now, "Apparently, the hammer works!");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                return false;
            }
        }

        public void ResetError()
        {
            Console.WriteLine($@"The error [{_errorRegistrationStatus.ErrorIdentifier}] is now resolved.");
            _errorRegistrationStatus = ErrorRegistrationStatus.NilStatus;
        }


        public void ForceClearPowerDownCommand()
        {
            _shutdownCommandRequestStatus = _shutdownCommandRequestStatus.ForceReset();
            if (_currentCommand == CommandIds.Shutdown)
                _currentCommand = null;
        }

        public void ForceClearDryStatusAndAnyCurrentCommandIfDry()
        {
            _dryCommandStatus = _dryCommandStatus.ForceReset();
            if (_currentCommand == CommandIds.Dry) _currentCommand = null;
        }


        private CommandRequestStatus _activationCommandStatus = new CommandRequestStatus(CommandIds.PowerUp);
        private CommandRequestStatus _shutdownCommandRequestStatus = new CommandRequestStatus(CommandIds.Shutdown);
        private CommandRequestStatus _washCommandStatus = new CommandRequestStatus(CommandIds.Wash);
        private CommandRequestStatus _dryCommandStatus = new CommandRequestStatus(CommandIds.Dry);
        private CommandIds? _currentCommand;
        private CancelFlag _cancelFlag = CancelFlag.NilCancelFlag;
        private ErrorRegistrationStatus _errorRegistrationStatus = ErrorRegistrationStatus.NilStatus;
        private LaundryItems _loadedItem = LaundryItems.InvalidItem;
        
    }


    public enum CommandIds
    {
        PowerUp,
        Shutdown,
        Wash,
        Dry,
        StartPostErrorDiagnostic,
    }

    public enum CommandRequestStatusCode
    {
        Nil = 0,
        Requested,
        Refused,
        Pending,
        CancellationRequested,
        Completed,
        Faulted,
        Cancelled,
    }

    public enum ErrorRegistrationStatusCode
    {
        Nil,
        Registered,
        Processed,
        Cleared
    }


    public enum CancelState : byte
    {
        Nil=0,
        Requested,
        Pending,
        Complete,
        
    }

    [VaultSafe]
    public readonly struct CancelFlag : IEquatable<CancelFlag>, IComparable<CancelFlag>
    {
        public static ref readonly CancelFlag NilCancelFlag => ref TheNilFlag;
        public static implicit operator CancelState(CancelFlag convert) => convert._state;
        public CancelState State => _state;
        public  DateTime? TimeStamp => _timestamp;


        [Pure]
        public override string ToString() =>
            _state == CancelState.Nil
                ? _state.ToString()
                : _state + " set at [" +
                  (TimeStamp.HasValue ? TimeStamp.Value.ToString("O") : "UNKNOWN") + "].";

        [Pure]
        public CancelFlag RegisterRequest()
        {
            const CancelState wantsToBe = CancelState.Requested;
            const CancelState needsToBeNow = CancelState.Nil;

            CancelFlag? temp = PerformTransition(wantsToBe, needsToBeNow);
            if (temp == null) throw new InvalidOperationException(GenerateExceptionMessage(wantsToBe, needsToBeNow));
            return temp.Value;
        }

        [Pure]
        public CancelFlag RegisterPending()
        {
            const CancelState wantsToBe = CancelState.Pending;
            const CancelState needsToBeNow = CancelState.Requested;

            CancelFlag? temp = PerformTransition(wantsToBe, needsToBeNow);
            if (temp == null) throw new InvalidOperationException(GenerateExceptionMessage(wantsToBe, needsToBeNow));
            return temp.Value;
        }

        [Pure]
        public CancelFlag RegisterComplete()
        {
            const CancelState wantsToBe = CancelState.Complete;
            const CancelState needsToBeNow = CancelState.Pending;

            CancelFlag? temp = PerformTransition(wantsToBe, needsToBeNow);
            if (temp == null) throw new InvalidOperationException(GenerateExceptionMessage(wantsToBe, needsToBeNow));
            return temp.Value;
        }
        [Pure]
        public CancelFlag? TryRegisterRequest()
        {
            const CancelState wantsToBe = CancelState.Requested;
            const CancelState needsToBeNow = CancelState.Nil;
            return PerformTransition(wantsToBe, needsToBeNow);
        }
        [Pure]
        public CancelFlag? TryRegisterPending()
        {
            const CancelState wantsToBe = CancelState.Pending;
            const CancelState needsToBeNow = CancelState.Requested;
            return PerformTransition(wantsToBe, needsToBeNow);
        }
        [Pure]
        public CancelFlag? TryRegisterComplete()
        {

            const CancelState wantsToBe = CancelState.Complete;
            const CancelState needsToBeNow = CancelState.Pending;
            return PerformTransition(wantsToBe, needsToBeNow);
        }

        [Pure]
        public ref readonly CancelFlag Reset() => ref NilCancelFlag;

        public static bool operator >(in CancelFlag lhs, in CancelFlag rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in CancelFlag lhs, in CancelFlag rhs) => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in CancelFlag lhs, in CancelFlag rhs) => !(lhs < rhs);
        public static bool operator <=(in CancelFlag lhs, in CancelFlag rhs) => !(lhs > rhs);
        public static bool operator ==(in CancelFlag lhs, in CancelFlag rhs) =>
            lhs._state == rhs._state && lhs._timestamp == rhs._timestamp;
        public static bool operator !=(in CancelFlag lhs, in CancelFlag rhs) => !(lhs == rhs);
        public override bool Equals(object other) => other is CancelFlag cf && cf == this;
        public int CompareTo(CancelFlag other) => Compare(in this, in other);
        public bool Equals(CancelFlag other) => other == this;

        public override int GetHashCode()
        {
            int hash = _state.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ _timestamp.GetHashCode();
            }

            return hash;
        }

        [Pure]
        private CancelFlag? PerformTransition(CancelState wantsToBe, CancelState needsToBeNow) =>
            _state == needsToBeNow ? (CancelFlag?) new CancelFlag(wantsToBe, TimeStampSource.Now) : null;

        private static string GenerateExceptionMessage(CancelState wantsToBe, CancelState needsToBeNow) =>
            $"Valid transition to {wantsToBe.ToString()} only from {needsToBeNow.ToString()}";

        private static int Compare(in CancelFlag lhs, in CancelFlag rhs)
        {
            byte lState = (byte) lhs._state;
            byte rState = (byte) rhs._state;
            int stateComparison = lState == rState ? 0 : (lState > rState ? 1 : -1);
            return stateComparison == 0 ? CompareTimeStamps(in lhs._timestamp, in rhs._timestamp) : 0;
        }

        private static int CompareTimeStamps(in DateTime? l, in DateTime? r)
        {
            DateTime ls = l ?? DateTime.MinValue;
            DateTime rs = r ?? DateTime.MinValue;
            return ls == rs ? 0 : (ls > rs ? 1 : -1);
        }

        private CancelFlag(CancelState cs, DateTime? ts)
        {
            _state = cs;
            _timestamp = ts;
        }

        
        private static readonly CancelFlag TheNilFlag = new CancelFlag(CancelState.Nil, null);
        private readonly DateTime? _timestamp;
        private readonly CancelState _state;
    }



    internal static class NullableTsHelper
    {

        
        public static int CompareNullableTimeStamps(DateTime? lts, DateTime? rts)
        {
            if (!lts.HasValue && !rts.HasValue) return 0;
            if (!lts.HasValue) return -1;
            if (!rts.HasValue) return 1;

            return lts.Value.CompareTo(rts.Value);
        }
        
    }
}
