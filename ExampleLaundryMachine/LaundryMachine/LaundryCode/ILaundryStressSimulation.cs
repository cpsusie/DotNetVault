using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public interface ILaundryStressSimulation : IDisposable
    {
        event EventHandler<SimulationEndedEventArgs> SimulationEnded;
        event EventHandler<ExceptionEventArgs> SimulationFaulted;
        bool IsDisposed { get; }
        int MachineCount { get; }
        public TimeSpan AddOneDampTime { get; }
        public TimeSpan RemoveOneDirtTime { get; }
        public TimeSpan RemoveOneDampTime { get; }
        [NotNull] ImmutableSortedDictionary<Guid, LaundryItems> OriginalDirtyLaundry { get; }
        [NotNull] [ItemNotNull] IReadOnlyList<ILaundryRobot> LoaderRobots { get; }
        [NotNull] [ItemNotNull] IReadOnlyList<ILaundryRobot> UnloaderRobots { get; }
        [NotNull] [ItemNotNull] public ImmutableList<IPublishLaundryMachineEvents> LaundryEventPublisher { get; }
        [NotNull] [ItemNotNull] public ImmutableList<LaundryMachineVault> LaundryMachines { get; }
        [NotNull] public ILaundryRepository CleanBin { get; }
        [NotNull] public ILaundryRepository DirtyBin { get; }
        [NotNull] [ItemNotNull] IEnumerable<ILaundryRepository> LaundryBins { get; }
        void StartSimulation();
        (bool Success, LaundrySimulationResults Result) TryGetCurrentResultStatus(TimeSpan? timeout = null);
        (bool Success, string Explanation) EvaluateResults();
        Task<(bool Success, string Explanation)> EvaluateResultsAsync();
    }
}