using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryStatusVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
using LockedLaundryStatus = LaundryMachine.LaundryCode.LockedLsf;
namespace LaundryMachine.LaundryCode
{
    public class LaundryMachine : ILaundryMachine
    {
        internal static LaundryMachine CreateLaundryMachine(TimeSpan addOneUnitDamp, TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp)
        {
            if (addOneUnitDamp <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(addOneUnitDamp), addOneUnitDamp,
                    @"Parameter must be positive.");
            if (removeOneUnitDirt <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(removeOneUnitDirt), removeOneUnitDirt,
                    @"Parameter must be positive.");
            if (removeOneUnitDamp <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(removeOneUnitDamp), removeOneUnitDamp,
                    @"Parameter must be positive.");
            return new LaundryMachine(addOneUnitDamp, removeOneUnitDirt, removeOneUnitDamp);
        }

        public long MachineId { get; }
        public LaundryMachineStateCode StateCode => _stateCodeForDisplay;
        public event EventHandler<LaundryMachineStatusEventArgs> MachineStatusUpdated;
        public event EventHandler<StateChangedEventArgs<LaundryMachineStateCode>> LaundryMachineChangedState;
        public event EventHandler Terminated;
        public event EventHandler<LaundryMachineAccessTimeoutEventArgs> AccessToLaundryMachineTimedOut;
        public event EventHandler<LaundryLoadedUnloadEventArgs> LaundryLoadedOrUnloaded; 
        public TimeSpan TimeToAddOneUnitDampness { get; }
        public TimeSpan TimeToRemoveOneUnitDirt { get; }
        public TimeSpan TimeToRemoveOneUnitDampness { get; }
        protected LaundryMachine(TimeSpan addOneUnitDamp, TimeSpan removeOneUnitDirt, TimeSpan removeOneUnitDamp)
        {
            LaundryStateMachine stateMachine = null;
            try
            {
                TimeToAddOneUnitDampness = addOneUnitDamp;
                TimeToRemoveOneUnitDirt = removeOneUnitDirt;
                TimeToRemoveOneUnitDampness = removeOneUnitDamp;
                MachineId = Interlocked.Increment(ref s_idCounter);
                _eventRaiser = EventRaisingThread.CreateEventRaiser($"EventRaiserLm{MachineId}");
                stateMachine = new LaundryStateMachine(_delayAtBottom, _maxNoYieldBeforeWait, addOneUnitDamp,
                    removeOneUnitDirt, removeOneUnitDamp);
                _flagVault = stateMachine.FlagVault;
                _stateVault = stateMachine.StateVault;
                 stateMachine.Disposed += _stateMachine_Disposed;
                 stateMachine.Terminated += _stateMachine_Terminated;
                 stateMachine.TransitionPredicateTrue += _stateMachine_TransitionPredicateTrue;
                 stateMachine.StateChanged += _stateMachine_StateChanged;
                 stateMachine.UnexpectedExceptionThrown += _stateMachine_UnexpectedExceptionThrown;
                _stateCodeForDisplay = _stateVault.CopyCurrentValue(TimeSpan.FromSeconds(2));
                Debug.Assert(_stateCodeForDisplay == LaundryMachineStateCode.PoweredDown);
                stateMachine.StartStateMachine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                try
                {
                    if (stateMachine != null)
                    {
                        _stateMachine.Disposed -= _stateMachine_Disposed;
                        _stateMachine.Terminated -= _stateMachine_Terminated;
                        _stateMachine.TransitionPredicateTrue -= _stateMachine_TransitionPredicateTrue;
                        _stateMachine.StateChanged -= _stateMachine_StateChanged;
                        _stateMachine.UnexpectedExceptionThrown -= _stateMachine_UnexpectedExceptionThrown;
                        _stateMachine.Dispose();
                    }
                }
                catch (Exception ex2)
                {
                    Console.Error.WriteLineAsync(ex2.ToString());
                    Environment.Exit(-1);
                    throw;
                }
                throw;
            }
            _stateMachine = stateMachine;
            Debug.Assert(_stateMachine != null);
        }

        public bool TurnOnMachine() => ExecutePossibleTimeoutRoutine(() =>
        {
            bool ret = false;
            using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
            if (stateCodeLock.Value == LaundryMachineStateCode.PoweredDown || stateCodeLock.Value == LaundryMachineStateCode.Empty)
            {
                using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                ret = flagLock.RegisterPowerOnCommand(stateCodeLock.Value);
            }

            return ret;
        }, nameof(TurnOnMachine));

        public bool TurnOffMachine() => ExecutePossibleTimeoutRoutine(() =>
        {
            bool ret = false;
            using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
            if (stateCodeLock.Value == LaundryMachineStateCode.Empty ||
                stateCodeLock.Value == LaundryMachineStateCode.Full)
            {
                using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                ret = flagLock.RegisterShutdownCommand(in stateCodeLock);
            }

            return ret;
        }, nameof(TurnOffMachine));

        public (Guid? LoadedLaundry, bool Cycled) LoadAndCycle(in LaundryItems laundry)
        {
            LaundryItems l = laundry;
            return ExecutePossibleTimeoutRoutine(() =>
            {
                Guid? loaded;
                bool cycleStart;
                {
                    using (var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait))
                    {
                        if (stateCodeLock.Value == LaundryMachineStateCode.Empty)
                        {
                            using (LockedLaundryStatus flagLock = _flagVault.SpinLock(_machineCommandMaxWait))
                            {
                                loaded = flagLock.LoadLaundry(l);
                                if (loaded != null)
                                {
                                    OnLoadedOrUnloaded(TimeStampSource.Now, l, true);
                                }
                            }
                        }
                        else
                        {
                            loaded = null;
                        }
                    }
                }
                if (loaded != null)
                {
                    ulong currentCount;
                    WaitForFull(_stateVault, MachineId, _cts.Token);
                    using (var stateLock = _stateVault.SpinLock(_machineCommandMaxWait))
                    using (LockedLaundryStatus flagLock = _flagVault.SpinLock(_machineCommandMaxWait))
                    {
                        currentCount = _stateMachine.StateChangeCount;
                        Debug.Assert(stateLock.Value == LaundryMachineStateCode.Full && flagLock.LoadedLaundryItem != LaundryItems.InvalidItem);
                        if (l.SoiledFactor == 0 && l.Dampness == 0)
                        {
                            cycleStart = false;
                        }
                        else
                        {
                            cycleStart = l.SoiledFactor != 0
                                ? flagLock.RegisterWashDryCommand()
                                : flagLock.RegisterDryCommand();
                        }
                    }

                    if (cycleStart)
                    {
                        WaitForStateChange(currentCount, _stateMachine, _cts.Token, MachineId);
                    }

                }
                else
                {
                    cycleStart = false;
                }
                return (loaded, cycleStart);
            }, nameof(LoadAndCycle));

            

            static void WaitForStateChange(ulong count, LaundryStateMachine lsm, CancellationToken token, long machineId)
            {
                DateTime startAt = TimeStampSource.Now;
                Debug.WriteLine($"At [{startAt:O}] Waiting for a state change increment on machine {machineId}.");
                DateTime updateAfter = startAt + TimeSpan.FromSeconds(20);
                while (lsm.StateChangeCount <= count)
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(TimeSpan.FromMilliseconds(50));
                    token.ThrowIfCancellationRequested();
                    DateTime now = TimeStampSource.Now;
                    if (now > updateAfter)
                    {
                        Debug.WriteLine($"At [{now:O}], still waiting for state change on machine [{machineId}].");
                        updateAfter = now + TimeSpan.FromSeconds(20);
                    }
                }
            }

            static void WaitForFull(BasicVault<LaundryMachineStateCode> v, long id, CancellationToken token)
            {
                TimeSpan maxWait = TimeSpan.FromSeconds(20);
                bool isFull = false;
                DateTime startAt = TimeStampSource.Now;
                DateTime quitAt = TimeStampSource.Now + maxWait;
                while (!isFull)
                {
                    var tryGet = v.TryCopyCurrentValue(TimeSpan.FromMilliseconds(50));
                    isFull = tryGet.success && tryGet.value == LaundryMachineStateCode.Full;
                    if (!isFull)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(50));
                        token.ThrowIfCancellationRequested();
                    }

                    DateTime now = TimeStampSource.Now;
                    if (now >= quitAt)
                    {
                        token.ThrowIfCancellationRequested();
                        Debug.WriteLine($"After {(now-startAt).TotalSeconds:F3} seconds on machine {id}, still waiting for full...");
                        quitAt = now + TimeSpan.FromSeconds(20);
                    }
                }

                if (!isFull)
                {
                    throw new StateLogicErrorException("After loading laundry, it should go to full quickly ... 20 seconds+ means something wrong.");
                }
            }
        }

        public Guid? LoadLaundry(in LaundryItems laundry)
        {
            LaundryItems l = laundry;
            return ExecutePossibleTimeoutRoutine(() =>
            {
                Guid? ret;
                using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
                if (stateCodeLock.Value == LaundryMachineStateCode.PoweredDown ||
                    stateCodeLock.Value == LaundryMachineStateCode.Empty)
                {
                    using LockedLaundryStatus flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                    ret = flagLock.LoadLaundry(l);
                    if (ret != null)
                    {
                        OnLoadedOrUnloaded(TimeStampSource.Now, l, true);
                    }
                }
                else
                {
                    ret = null;
                }

                return ret;
            }, nameof(LoadLaundry), null);
        }

        public LaundryItems? UnloadLaundry(in Guid id)
        {
            Guid myId = id;
            return ExecutePossibleTimeoutRoutine(() =>
            {
                LaundryItems? ret;
                using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
                if (stateCodeLock.Value == LaundryMachineStateCode.PoweredDown ||
                    stateCodeLock.Value == LaundryMachineStateCode.Full)
                {
                    using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                    ret = flagLock.UnloadLaundry(myId);
                    if (ret != null)
                    {
                        OnLoadedOrUnloaded(TimeStampSource.Now, ret.Value, false);
                    }
                }
                else
                {
                    ret = null;
                }
                return ret;
            }, nameof(UnloadLaundry), null);
        }

        public LaundryItems? UnloadAnyLaundry() => ExecutePossibleTimeoutRoutine(() =>
        {
            LaundryItems? ret;
            using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
            if (stateCodeLock.Value == LaundryMachineStateCode.PoweredDown ||
                stateCodeLock.Value == LaundryMachineStateCode.Full)
            {
                using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                ret = flagLock.UnloadLaundry();
                if (ret != null)
                {
                    OnLoadedOrUnloaded(TimeStampSource.Now, ret.Value, false);
                }
            }
            else
            {
                ret = null;
            }

            return ret;
        }, nameof(UnloadAnyLaundry), null);
        


        public bool InitiateWash() => ExecutePossibleTimeoutRoutine(() =>
        {
            bool ret = false;
            using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
            if (stateCodeLock.Value == LaundryMachineStateCode.Full)
            {
                using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                ret = flagLock.RegisterWashCommand();
            }
            return ret;
        }, nameof(InitiateWash));


        public bool InitiateDry() => ExecutePossibleTimeoutRoutine(() =>
        {
            bool ret = false;
            using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
            if (stateCodeLock.Value == LaundryMachineStateCode.Full)
            {
                using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                ret = flagLock.RegisterDryCommand();
            }
            return ret;
        }, nameof(InitiateDry));

        public bool InitiateWashDry() => ExecutePossibleTimeoutRoutine(() =>
        {
            bool ret = false;
            using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
            if (stateCodeLock.Value == LaundryMachineStateCode.Full)
            {
                using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                ret = flagLock.RegisterWashDryCommand();
            }
            return ret;
        }, nameof(InitiateWashDry));

        public bool Abort() => ExecutePossibleTimeoutRoutine(() =>
        {
            bool ret = false;
            using var stateCodeLock = _stateVault.SpinLock(_machineCommandMaxWait);
            if (stateCodeLock.Value == LaundryMachineStateCode.Washing ||
                stateCodeLock.Value == LaundryMachineStateCode.Drying)
            {
                using var flagLock = _flagVault.SpinLock(_machineCommandMaxWait);
                ret = flagLock.RegisterCancelCurrentTask();
            }

            return ret;
        }, nameof(Abort));

        public LaundryMachineStateCode? QueryExactStateCode(TimeSpan timeout)
        {
            try
            {
                using var stateCodeLock = _stateVault.SpinLock(timeout);
                return stateCodeLock.Value;
            }
            catch (TimeoutException)
            {
                return null;
            }
        }
        public LaundryMachineStateCode? QueryExactStateCode(TimeSpan timeout, CancellationToken token)
        {
            try
            {
                using var stateCodeLock = _stateVault.SpinLock(timeout, token);
                return stateCodeLock.Value;
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~LaundryMachine() => Dispose(false);

        protected virtual void OnAccessToLaundryMachineTimedOut(LaundryMachineAccessTimeoutEventArgs e) =>
            PostAction(() =>
                AccessToLaundryMachineTimedOut?.Invoke(this, e));
        protected virtual void OnMachineStatusUpdated(LaundryMachineStatusEventArgs e) => PostAction(()=>
            MachineStatusUpdated?.Invoke(this, e));
        protected virtual void OnLaundryMachineChangedState(StateChangedEventArgs<LaundryMachineStateCode> e) 
            => PostAction(()=>LaundryMachineChangedState?.Invoke(this, e));
        protected virtual void OnTerminated() => PostAction(() => Terminated?.Invoke(this, EventArgs.Empty));
        protected virtual void OnLoadedOrUnloaded(DateTime ts, LaundryItems item, bool loaded) => PostAction(() =>
        {
            LaundryLoadedUnloadEventArgs e = new LaundryLoadedUnloadEventArgs(ts, MachineId, loaded, item.ItemId, item.ItemDescription);
            LaundryLoadedOrUnloaded?.Invoke(this, e);
        });
        protected void PostAction([NotNull] Action postMe )
        {
            if (postMe == null) throw new ArgumentNullException(nameof(postMe));
            if (_eventRaiser.ThreadActive && !_eventRaiser.IsDisposed)
            {
                try
                {
                    _eventRaiser.AddAction(postMe);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
            }
            else
            {
                try
                {
                    postMe();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                _cts.Cancel();
                
                if (!_stateMachine.IsDisposed && _smDisposed.TrySet())
                {
                    try
                    {
                        _stateMachine.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync(ex.ToString());
                    }
                }

                IEventRaiser raiser = _eventRaiser;

                ActionExtensions.ExecuteActionLogExceptionIgnore(() => raiser.Dispose());

                MachineStatusUpdated = null;
                LaundryMachineChangedState = null;
                Terminated = null;
                AccessToLaundryMachineTimedOut = null;
                LaundryLoadedOrUnloaded = null;
                _cts.Dispose();
            }
            _disposed.TrySet();
        }

        /// <summary>
        /// comparison is based on MachineId ONLY
        /// </summary>
        /// <param name="other">the other one</param>
        /// <returns>less than zero -> this object is less than other;
        /// 0 -> this object compares as equal to other,
        /// greater than zero -> this object is greater than other</returns>
        public int CompareTo(IVsLaundryMachineFunctionality other)
        {
            if (other == null) return -1;
            return MachineId.CompareTo(other.MachineId);
        }

        private void _stateMachine_UnexpectedExceptionThrown(object sender, UnexpectedStateMachineFaultEventArgs e)
        {
            if (e != null)
            {
                OnMachineStatusUpdated(
                    LaundryMachineStatusEventArgs.CreateStatusEventArgs(
                        nameof(LaundryStateMachine.UnexpectedExceptionThrown), e, TimeStampSource.Now));
            }
        }

        private void _stateMachine_StateChanged(object sender, StateChangedEventArgs<LaundryMachineStateCode> e)
        {
            if (e != null)
            {
                _stateCodeForDisplay = e.NewState;
                OnLaundryMachineChangedState(e);
            }    
        }

        private void _stateMachine_TransitionPredicateTrue(object sender, TransitionPredicateTrueEventArgs<LaundryMachineStateCode> e)
        {
            if (e != null)
            {
                OnMachineStatusUpdated(
                    LaundryMachineStatusEventArgs.CreateStatusEventArgs(nameof(_stateMachine.TransitionPredicateTrue),
                        e, TimeStampSource.Now));
            }
        }

        private void _stateMachine_Terminated(object sender, EventArgs e)
        {
            if (_smTerminated.TrySet())
            {
                OnTerminated();
            }
        }

        private void _stateMachine_Disposed(object sender, EventArgs e)
        {
            if (_smDisposed.TrySet())
            {
                OnMachineStatusUpdated(LaundryMachineStatusEventArgs.CreateStatusEventArgs(
                    nameof(_stateMachine.Disposed),
                    EventArgs.Empty, TimeStampSource.Now));
            }
        }

        private T ExecutePossibleTimeoutRoutine<T>([NotNull] Func<T> func, [NotNull] string caller, T valOnError = default)
        {
            T ret;
            DateTime startAt = TimeStampSource.Now;
            try
            {
                ret = func();
            }
            catch (TimeoutException ex)
            {
                DateTime timeout = TimeStampSource.Now;
                LaundryMachineAccessTimeoutEventArgs args =
                    LaundryMachineAccessTimeoutEventArgs.CreateLmAccessTimeoutTimeStamp(MachineId, startAt,
                        timeout, ex, caller);
                OnAccessToLaundryMachineTimedOut(args);
                ret = valOnError;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                throw;
            }
            return ret;
        }

        private readonly TimeSpan _machineCommandMaxWait = TimeSpan.FromSeconds(4);
        private volatile LaundryMachineStateCode _stateCodeForDisplay;
        private readonly int _maxNoYieldBeforeWait = 10;
        private readonly TimeSpan _delayAtBottom = TimeSpan.FromMilliseconds(100);
        [NotNull] private readonly LaundryStateMachine _stateMachine;
        [NotNull] private readonly LaundryStatusVault _flagVault;
        [NotNull] private readonly BasicVault<LaundryMachineStateCode> _stateVault;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly IEventRaiser _eventRaiser;
        private LocklessSetOnceFlagVal _smTerminated = new LocklessSetOnceFlagVal();
        private LocklessSetOnceFlagVal _smDisposed = new LocklessSetOnceFlagVal();
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        // ReSharper disable once InconsistentNaming
        private static long s_idCounter;
    }

    public sealed class LaundryLoadedUnloadEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; }
        public long MachineNumber { get; }
        public bool Loaded { get; }
        public bool Unloaded => !Loaded;
        public Guid LaundryId { get; }
        [NotNull] public string LaundryDescription { get; }

        public LaundryLoadedUnloadEventArgs(DateTime timeStamp, long machineNumber, bool loaded,  Guid laundryId, string description)
        {
            TimeStamp = timeStamp;
            MachineNumber = machineNumber;
            Loaded = loaded;
            LaundryId = laundryId;
            LaundryDescription = description ?? throw new ArgumentNullException(nameof(description));
        }

        public override string ToString() =>
            $"At [{TimeStamp:O}] laundry with id [{Loaded}] was " +
            $"{(Loaded ? "Loaded" : "Unloaded")} into machine# {MachineNumber}.";

    }
}
