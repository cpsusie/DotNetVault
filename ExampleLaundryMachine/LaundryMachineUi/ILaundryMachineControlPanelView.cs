using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using LaundryMachine.LaundryCode;

namespace LaundryMachineUi
{
    public interface ILaundryMachineControlPanelView : IDisposable
    {
        bool ShowCommandButtons { get; set; }
        bool IsDisposed { get; }
        long LaundryMachineNumber { get; }
        LaundryMachineStateCode StateCode { get; }
        void SupplyLaundryMachine([NotNull] LaundryMachineVault mv, [NotNull] IPublishLaundryMachineEvents pe);
        (bool Success, LaundryMachineVault ReleasedMachine, IPublishLaundryMachineEvents EventPublisher) TryReleaseMachine();
        Task<LaundryItems?> UnloadLaundryAsync(in Guid id);
        Task<LaundryItems?> UnloadAnyLaundryAsync();
        Task<Guid?> LoadLaundryAsync(in LaundryItems item);
        Task<(Guid? LoadedLaundry, bool Cycled)> LoadAndCycleAsync(in LaundryItems item);
        Task<bool> ExecuteWashDryAsync();
        Task<bool> ExecuteDryAsync();
        Task<bool> ExecuteAbortAsync();
    }
}