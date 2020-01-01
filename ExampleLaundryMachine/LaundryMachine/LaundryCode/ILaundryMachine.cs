using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using JetBrains.Annotations;


namespace LaundryMachine.LaundryCode
{
       
    public interface IPublishLaundryMachineEvents : IDisposable
    {
        event EventHandler<LaundryLoadedUnloadEventArgs> LaundryLoadedOrUnloaded;
        event EventHandler<LaundryMachineStatusEventArgs> MachineStatusUpdated;
        event EventHandler<StateChangedEventArgs<LaundryMachineStateCode>> LaundryMachineChangedState;
        event EventHandler Terminated;
        event EventHandler<LaundryMachineAccessTimeoutEventArgs> AccessToLaundryMachineTimedOut;

    }

    public interface IVsLaundryMachineFunctionality : IDisposable, IComparable<IVsLaundryMachineFunctionality>
    {
        LaundryMachineStateCode StateCode { get; }
        long MachineId { get; }
        bool TurnOnMachine();
        bool TurnOffMachine();
        Guid? LoadLaundry(in LaundryItems laundry);
        (Guid? LoadedLaundry, bool Cycled) LoadAndCycle(in LaundryItems laundry);
        LaundryItems? UnloadLaundry(in Guid id);
        LaundryItems? UnloadAnyLaundry();
        bool InitiateWash();
        bool InitiateDry();
        bool InitiateWashDry();
        bool Abort();
        LaundryMachineStateCode? QueryExactStateCode(TimeSpan timeout);
        LaundryMachineStateCode? QueryExactStateCode(TimeSpan timeout, CancellationToken token);
    }

    public interface ILaundryMachine : IVsLaundryMachineFunctionality, IPublishLaundryMachineEvents
    {

    }

   

    internal sealed class LmEventPublisher : IPublishLaundryMachineEvents
    {
        internal static LmEventPublisher CreateEventPublisher([NotNull] LaundryMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            return new LmEventPublisher(machine);
        }

        public event EventHandler<LaundryLoadedUnloadEventArgs> LaundryLoadedOrUnloaded;
        public event EventHandler<LaundryMachineStatusEventArgs> MachineStatusUpdated;
        public event EventHandler<StateChangedEventArgs<LaundryMachineStateCode>> LaundryMachineChangedState;
        public event EventHandler Terminated;
        public event EventHandler<LaundryMachineAccessTimeoutEventArgs> AccessToLaundryMachineTimedOut;

        public void Dispose() => Dispose(true);
       
        
        private LmEventPublisher([NotNull] LaundryMachine machine)
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
            _sender = machine.MachineId;
            _machine.AccessToLaundryMachineTimedOut += _machine_AccessToLaundryMachineTimedOut;
            _machine.LaundryLoadedOrUnloaded += _machine_LaundryLoadedOrUnloaded;
            _machine.LaundryMachineChangedState += _machine_LaundryMachineChangedState;
            _machine.MachineStatusUpdated += _machine_MachineStatusUpdated;
            _machine.Terminated += _machine_Terminated;
        }

        private void _machine_Terminated(object sender, EventArgs e) => Terminated?.Invoke(_sender, e);


        private void _machine_MachineStatusUpdated(object sender, LaundryMachineStatusEventArgs e) =>
            MachineStatusUpdated?.Invoke(_sender, e);

        private void _machine_LaundryMachineChangedState(object sender,
            StateChangedEventArgs<LaundryMachineStateCode> e) =>
            LaundryMachineChangedState?.Invoke(_sender, e);


        private void _machine_LaundryLoadedOrUnloaded(object sender, LaundryLoadedUnloadEventArgs e) =>
            LaundryLoadedOrUnloaded?.Invoke(_sender, e);


        private void _machine_AccessToLaundryMachineTimedOut(object sender, LaundryMachineAccessTimeoutEventArgs e) =>
            AccessToLaundryMachineTimedOut?.Invoke(_sender, e);

        private void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                _machine.AccessToLaundryMachineTimedOut -= _machine_AccessToLaundryMachineTimedOut;
                _machine.LaundryLoadedOrUnloaded -= _machine_LaundryLoadedOrUnloaded;
                _machine.LaundryMachineChangedState -= _machine_LaundryMachineChangedState;
                _machine.MachineStatusUpdated -= _machine_MachineStatusUpdated;
                _machine.Terminated -= _machine_Terminated;
                LaundryLoadedOrUnloaded = null;
                MachineStatusUpdated = null;
                LaundryMachineChangedState = null;
                Terminated = null;
                AccessToLaundryMachineTimedOut = null;
            }
            _disposed.TrySet();
        }

        private LocklessSetOnceFlagVal _disposed;
        private readonly LaundryMachine _machine;
        private readonly long _sender;
    }


}