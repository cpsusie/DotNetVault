using DotNetVault.Vaults;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryMachine.LaundryCode
{
    public class LaundryStressSimulation : ILaundryStressSimulation
    {
        public const uint DefaultDirtyArticles = 200;
        public static readonly UIntRange DirtyRange = new UIntRange(1, 200);
        #region Factory Method

        [NotNull]
        public static LaundryStressSimulation CreateSimulation(TimeSpan addOneDamp, TimeSpan removeOneDirt,
            TimeSpan removeOneDamp) => CreateSimulation(addOneDamp, removeOneDirt, removeOneDamp, DefaultDirtyArticles);

        public static LaundryStressSimulation CreateSimulation(TimeSpan addOneDamp, TimeSpan removeOneDirt, TimeSpan removeOneDamp, uint dirtyArticles)
        {
            DirtyRange.Validate(nameof(dirtyArticles), dirtyArticles);
            LaundryStressSimulation simulation = null;
            try
            {
                simulation = new LaundryStressSimulation(addOneDamp, removeOneDirt, removeOneDamp, dirtyArticles);
                simulation.Init();
            }
            catch (StateLogicErrorException logicError)
            {
                Console.Error.WriteLineAsync(logicError.ToString());
                try
                {
                    simulation?.Dispose();
                }
                catch (Exception e2)
                {
                    Console.Error.WriteLineAsync(e2.ToString());
                }
                TerminationHelper.TerminateApplication(logicError.Message, logicError);
                throw;
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                try
                {
                    simulation?.Dispose();
                }
                catch (Exception e2)
                {
                    Console.Error.WriteLineAsync(e2.ToString());
                }
                throw;
            }
            return simulation;
        } 
        #endregion

        #region Properties
        public event EventHandler<SimulationEndedEventArgs> SimulationEnded;
        public event EventHandler<ExceptionEventArgs> SimulationFaulted;
        /// <summary>
        /// Get the current status of the results
        /// </summary>
        /// <exception cref="TimeoutException">Couldn't obtain the resource within 250 milliseconds</exception>
        public LaundrySimulationResults CurrentResultStatus =>
            _simulationResult.CopyCurrentValue(_simulationResult.DefaultTimeout);
        public ImmutableSortedDictionary<Guid, LaundryItems> OriginalDirtyLaundry => _initialLaundryItems.Value;
        public IReadOnlyList<ILaundryRobot> LoaderRobots => _loaderRobots.Value;
        public IReadOnlyList<ILaundryRobot> UnloaderRobots => _unloaderRobots.Value;
        public bool IsDisposed => _disposed.IsSet;
        public int MachineCount { get; private set; }
        public ImmutableList<IPublishLaundryMachineEvents> LaundryEventPublisher => _laundryMachines.Value;
        public ImmutableList<LaundryMachineVault> LaundryMachines => _wrappers.Value;
        public ILaundryRepository CleanBin => _cleanLaundry;
        public ILaundryRepository DirtyBin => _dirtyLaundry;
        public TimeSpan AddOneDampTime { get; }
        public TimeSpan RemoveOneDirtTime { get; }
        public TimeSpan RemoveOneDampTime { get; }

        public IEnumerable<ILaundryRepository> LaundryBins
        {
            get
            {
                yield return CleanBin;
                yield return DirtyBin;
            }
        } 
        #endregion

        #region CTOR
        protected LaundryStressSimulation(TimeSpan addOneDamp, TimeSpan removeOneDirt, TimeSpan removeOneDamp, uint dirtyArticles)
        {
            AddOneDampTime = addOneDamp;
            RemoveOneDirtTime = removeOneDirt;
            RemoveOneDampTime = removeOneDamp;
            _laundryMachines = new LocklessWriteOnce<ImmutableList<IPublishLaundryMachineEvents>>();
            _wrappers = new LocklessWriteOnce<ImmutableList<LaundryMachineVault>>();
            _loaderRobots = new LocklessLazyWriteOnce<ImmutableList<ILaundryRobot>>(InitLoaderRobots);
            _unloaderRobots = new LocklessLazyWriteOnce<ImmutableList<ILaundryRobot>>(InitUnloaderRobots);
            _initialLaundryItems = new LocklessLazyWriteOnce<ImmutableSortedDictionary<Guid, LaundryItems>>(InitLaundryItems);
            _dirtyArticles = dirtyArticles;
        } 
        #endregion

        #region Public Methods
        public void StartSimulation()
        {
            if (_startedYet.TrySet())
            {
                {
                    using var lck = _simulationResult.SpinLock();
                    lck.Value = lck.Value.Start(OriginalDirtyLaundry.Count);
                    Debug.Assert(lck.Value.InProgress);
                }
                //.Where(robot => robot.RobotId != LoaderRobots[1].RobotId).
                bool allStartedOk = LoaderRobots.Concat(UnloaderRobots).All(robot => robot.StartOrUnPause());
                if (!allStartedOk)
                {
                    EndSimulation();
                }
            }
            else
            {
                throw new InvalidOperationException("The simulation has already been started.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~LaundryStressSimulation() => Dispose(false);

        public (bool Success, LaundrySimulationResults Result) TryGetCurrentResultStatus(TimeSpan? timeout = null)
        {
            TimeSpan actualTimeout = timeout > TimeSpan.Zero ? timeout.Value : _simulationResult.DefaultTimeout;
            var temp = _simulationResult.TryCopyCurrentValue(actualTimeout);
            return (temp.success, temp.value);
        }

        public (bool Success, string Explanation) EvaluateResults() => DoEvaluation();

        public Task<(bool Success, string Explanation)> EvaluateResultsAsync() => Task.Run(DoEvaluation);

        #endregion

        #region Protected Methods

        protected virtual IEnumerable<(LaundryMachineVault MachineVault, IPublishLaundryMachineEvents EventPublisher)>
            InitLaundryMachines()
        {
            yield return LaundryMachineVault.CreateVaultAndEventPublisher(TimeSpan.FromSeconds(5), AddOneDampTime, RemoveOneDirtTime,
                RemoveOneDampTime);
            yield return LaundryMachineVault.CreateVaultAndEventPublisher(TimeSpan.FromSeconds(5), AddOneDampTime, RemoveOneDirtTime,
                RemoveOneDampTime);
            yield return LaundryMachineVault.CreateVaultAndEventPublisher(TimeSpan.FromSeconds(5), AddOneDampTime, RemoveOneDirtTime,
                RemoveOneDampTime);
        }
        protected virtual ImmutableList<ILaundryRobot> InitUnloaderRobots()
        {
            ILaundryRobot unloaderOne = UnloaderRobot.CreateUnloaderRobot("First Unloader Robot", LaundryMachines, LaundryBins);
            ILaundryRobot unloaderTwo = UnloaderRobot.CreateUnloaderRobot("Second Unloader Robot", LaundryMachines, LaundryBins);
            return ImmutableList.Create(unloaderOne, unloaderTwo);
        }

        protected virtual ImmutableList<ILaundryRobot> InitLoaderRobots()
        {
            ILaundryRobot loaderOne = LoaderRobot.CreateLoaderRobot("First Loader Robot", LaundryMachines, LaundryBins);
            ILaundryRobot loaderTwo = LoaderRobot.CreateLoaderRobot("Second Loader Robot", LaundryMachines, LaundryBins);
            return ImmutableList.Create(loaderOne, loaderTwo);
        }

        protected virtual (bool Success, string Explanation) DoEvaluation()
        {
            var result = _simulationResult.CopyCurrentValue(TimeSpan.FromMinutes(1));
            
            if (result.NotStartedYet) return (false, "The simulation has not yet been started.");
            if (result.InProgress)
            {
                return (false, 
                    $"The simulation is still in progress and has been in progress for {result.TimeSinceStarted.Seconds:F6} seconds.  There are {result.LaundryItemsToGo} dirty laundry items remaining.");
            }
            if (result.Finished)
            {
                var evalR = GetComparisonText(); 
                return (evalR.Passed,
                    $"The simulation was completed after {result.FinalElapsedTime.Seconds:F6} seconds.  There are {result.LaundryItemsToGo} dirty laundry items remaining. {Environment.NewLine} {evalR.Text}");
            }
            return
                (false ,
                    $"The simulation was terminated early, after {result.FinalElapsedTime.Seconds:F6} seconds.  At termination time there were {result.LaundryItemsToGo} remaining.");
            
        }

        private (bool Passed, string Text) GetComparisonText()
        {
            bool passed = true;
            if (!_cleanLaundry.IsDisposed)
            {
                _cleanLaundry.Dispose();
                Debug.Assert(_cleanLaundry.IsDisposed);
            }
            ImmutableDictionary<Guid, LaundryItems> cleanResults = _cleanLaundry.Dump().ToImmutableDictionary(li => li.ItemId, li => li);
            int expectedCount = _initialLaundryItems.Value.Count;
            int actualCount = cleanResults.Count;
            if (expectedCount != actualCount)
            {
                passed = false;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var kvp in _initialLaundryItems.Value)
            {

                bool gotMatch = cleanResults.TryGetValue(kvp.Key, out LaundryItems cleaned);
                if (gotMatch)
                {
                    if (cleaned.ItemDescription != kvp.Value.ItemDescription)
                    {
                        passed = false;
                        sb.AppendLine(
                            $"Item with id {kvp.Key}'s original description {kvp.Value.ItemDescription} does not match the final description {cleaned.ItemDescription}.");
                    }

                    if (cleaned.Dampness != 0)
                    {
                        passed = false;
                        sb.AppendLine(
                            $"Item with id {kvp.Key} is still damp.  Dampness: {cleaned.Dampness.ToString()}");
                    }

                    if (cleaned.SoiledFactor != 0)
                    {
                        passed = false;
                        sb.AppendLine(
                            $"Item with id {kvp.Key} is dirty.  Dirtyness: {cleaned.SoiledFactor.ToString()}");
                    }
                }
                else
                {
                    passed = false;
                    sb.AppendLine($"Laundry item with id {kvp.Key} was missing from the final result.");
                }
            }

            sb.AppendLine(passed
                ? "All items accounted for, match their original description and are dry and clean."
                : $"Failed.  See above for details.  Expected {expectedCount} items, but only found {actualCount} items in the clean receptacle.");

            return (passed, sb.ToString());

        }

        protected void Init()
        {
            var temp = InitLaundryMachines().ToImmutableArray();
            ImmutableList<IPublishLaundryMachineEvents> publisherList =
                ImmutableList.CreateRange(temp.Select(tpl => tpl.EventPublisher));
            ImmutableList<LaundryMachineVault> functionalityList = ImmutableList.CreateRange(temp.Select(tpl => tpl.MachineVault));
                            
            _laundryMachines.SetOrThrow(publisherList);
            _wrappers.SetOrThrow(functionalityList);

            Debug.Assert(_wrappers.IsSet && _wrappers.Value != null);
            Debug.Assert(_laundryMachines.IsSet && _laundryMachines.Value != null);

            MachineCount = LaundryMachines.Count;
            foreach (var item in OriginalDirtyLaundry.Values)
            {
                _dirtyLaundry.Add(in item);
            }

            long[] couldntTurnUsOn = (from vault in LaundryMachines
                let turnOnResult = TryTurnOn(vault)
                where !turnOnResult.TurnedOn
                select turnOnResult.MachineId).ToArray();


            if (couldntTurnUsOn.Length > 0)
            {
                throw new StateLogicErrorException(
                    $"Stress simulation unable to activate machine with id: [{couldntTurnUsOn.First().ToString()}].");
            }

            foreach (var unloader in UnloaderRobots)
            {
                var state = unloader.State;
                switch (state)
                {
                    case RobotState.Initialized:
                        unloader.StartRobot();
                        break;
                    case RobotState.Paused:
                        unloader.UnPauseRobot();
                        break;
                    default: 
                        throw new InvalidOperationException($"Cannot do anything to robot in the {state} state.");
                }
                if (unloader.State == RobotState.Initialized)
                    unloader.StartRobot();
            }

            (bool TurnedOn, long MachineId) TryTurnOn(LaundryMachineVault turnMeOn)
            {
                using var lck = turnMeOn.SpinLock(TimeSpan.FromMilliseconds(250));
                long id = lck.MachineId;
                bool turnedOn = lck.TurnOnMachine();
                return (turnedOn, id);
            }

            _cleanLaundry.ContentsChanged += _cleanLaundry_ContentsChanged;
        }

        protected virtual void OnSimulationEnded([NotNull] SimulationEndedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            _eventRaiser.AddAction(Action);

            void Action()
            {
                SimulationEnded?.Invoke(this, args);
            }
        }

        protected virtual void OnSimulationFaulted([NotNull] ExceptionEventArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            _eventRaiser.AddAction(Action);

            void Action()
            {
                try
                {
                    EndSimulation();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }
                SimulationFaulted?.Invoke(this, args);
            }
        }

        protected virtual void EndSimulation()
        {
            if (_endedYet.TrySet())
            {
                LaundrySimulationResults simResults;
                {
                    using var lck = _simulationResult.SpinLock();
                    if (lck.Value.InProgress)
                        lck.Value = lck.Value.TerminateEarly();
                    simResults = lck.Value;
                }

                try
                {

                    foreach (var robot in LoaderRobots.Concat(UnloaderRobots).Where(rbt => rbt?.IsDisposed == false))
                    {
                        robot.PauseRobot();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    BreakIfDebuggerAttached();
                }
                OnSimulationEnded(new SimulationEndedEventArgs(in simResults));
            }
        }

        protected virtual ImmutableSortedDictionary<Guid, LaundryItems> InitLaundryItems()
        {
            return ImmutableSortedDictionary.CreateRange(InitItems()
                .Select(li => new KeyValuePair<Guid, LaundryItems>(li.ItemId, li)));
            IEnumerable<LaundryItems> InitItems()
            {
                int cast;
                checked
                {
                    cast = (int) _dirtyArticles;
                }
                Random r = new Random();
                foreach (var name in NameArrayGenerator.CreateRandomNames(r, cast))
                {
                    byte dampness = (byte)r.Next(0, 11);
                    byte soiled = (byte)r.Next(50, 256);
                    int weightHundrethsOfKilos = r.Next(25, 501);
                    yield return LaundryItems.CreateLaundryItems($"{name}'s Laundry", weightHundrethsOfKilos / 100.0m,
                        soiled, dampness);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                if (_endedYet.TrySet())
                {
                    EndSimulation();
                }
                //Dispose Robots
                foreach (var robot in LoaderRobots.Concat(UnloaderRobots))
                {
                    robot.Dispose();
                }

                foreach (var machine in LaundryMachines)
                {
                    try
                    {
                        machine.Dispose();
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLineAsync(e.ToString());
                    }
                }
                _dirtyLaundry.Dispose();
                _cleanLaundry.Dispose();

                SimulationFaulted = null;
                SimulationEnded = null;
                _eventRaiser.Dispose();
            }
            _disposed.TrySet();
        } 
        #endregion
        
        #region Private Methods
        private void _cleanLaundry_ContentsChanged(object sender, LaundryRepositoryEventArgs e)
        {
            try
            {
                if (e.AddedToRepo)
                {
                    LaundrySimulationResults results;
                    {
                        using var lck = _simulationResult.SpinLock();
                        results = (lck.Value = lck.Value.AnotherItemCleaned());
                    }
                    if (results.Finished)
                    {
                        EndSimulation();
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                OnSimulationFaulted(new ExceptionEventArgs(ex, TimeStampSource.Now));
            }
            catch (TimeoutException ex) //I consider not getting this (rather low contention vault) within 250 ms to be sign of a serious problem meriting termination.
            {
                //Fail simulation ... contention for this resource should be low
                OnSimulationFaulted(new ExceptionEventArgs(ex, TimeStampSource.Now));
            }
        }

        [Conditional("DEBUG")]
        private void BreakIfDebuggerAttached()
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        } 
        #endregion

        #region Privates
        protected readonly uint _dirtyArticles;
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        [NotNull]
        private readonly BasicVault<LaundrySimulationResults> _simulationResult =
            new BasicVault<LaundrySimulationResults>(default, TimeSpan.FromMilliseconds(250));
        private LocklessSetOnceFlagVal _startedYet = new LocklessSetOnceFlagVal();
        private LocklessSetOnceFlagVal _endedYet = new LocklessSetOnceFlagVal();
        [NotNull]
        private readonly IEventRaiser _eventRaiser =
            EventRaisingThread.CreateEventRaiser("StressSimEventThread");
        [NotNull] private readonly LocklessLazyWriteOnce<ImmutableSortedDictionary<Guid, LaundryItems>> _initialLaundryItems;
        [NotNull] private readonly LocklessLazyWriteOnce<ImmutableList<ILaundryRobot>> _loaderRobots;
        [NotNull] private readonly LocklessLazyWriteOnce<ImmutableList<ILaundryRobot>> _unloaderRobots;
        [NotNull] private readonly LocklessWriteOnce<ImmutableList<IPublishLaundryMachineEvents>> _laundryMachines;
        [NotNull] private readonly LocklessWriteOnce<ImmutableList<LaundryMachineVault>> _wrappers; 
        [NotNull] private readonly ILaundryRepository _dirtyLaundry = LaundryRepository.CreateRepository("Dirty Laundry Bin", true);
        [NotNull]
        private readonly ILaundryRepository _cleanLaundry =
            LaundryRepository.CreateRepository("Clean Laundry Bin", false); 
        #endregion
    }

    

}
