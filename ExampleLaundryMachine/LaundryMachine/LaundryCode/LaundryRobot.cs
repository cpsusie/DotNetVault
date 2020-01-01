using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public abstract class LaundryRobot : ILaundryRobot
    {
        [NotNull] public string RobotName => _robotName;
        public RobotState State => _activityFlag;
        public event EventHandler<RobotActedEventArgs> RobotActed;
        public bool IsDisposed => _disposed.IsSet;
        public abstract LaundryRobotCategory RobotCategory { get; }
        public Guid RobotId { get; }
        protected string ConcreteTypeName => ConcreteType.Name;
        protected Type ConcreteType => _concreteType;
        protected ImmutableArray<ILaundryRepository> Repositories { get; }
        protected ImmutableArray<LaundryMachineVault> Machines { get; }

        public void StartRobot()
        {
            if (_activityFlag.SetFromInitializedToStartingUp())
            {
                _robotThread.Start(_cts.Token);

                DateTime startTimeOut = TimeStampSource.Now + TheMaxTimeToStart;
                while (State == RobotState.StartingUp && TimeStampSource.Now <= startTimeOut)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }

                if (State == RobotState.StartingUp)
                {
                    TerminationHelper.TerminateApplication(
                        $"The robot named {RobotName} could not start up within {TheMaxTimeToStart} milliseconds.");
                }
            }
            else
            {
                throw new InvalidOperationException("Thread already has been started up.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool PauseRobot() => _activityFlag.SetFromActiveToPausing();
        public bool UnPauseRobot() => _activityFlag.SetFromPausedToActive();

        public (bool AccessOk, LaundryItems? HeldLaundry) QueryHeldLaundry()
        {
            try
            {
                using var lck = _heldLaundryItem.SpinLock();
                LaundryItems? itm = lck.Value;
                return (true, itm);
            }
            catch (TimeoutException)
            {
                return (false, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                return (false, null);
            }
        }
        public (bool AccessOk, LaundryItems? RemovedLaundry) RemoveAnyLaundry()
        {
            if (_disposed.IsSet) throw new InvalidOperationException();
            try
            {
                using var lck = _heldLaundryItem.SpinLock();
                LaundryItems? itm = lck.Value;
                lck.Value = null;
                return (true, itm);
            }
            catch (TimeoutException)
            {
                return (false, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                return (false, null);
            }
        }

     
        protected LaundryRobot([NotNull] string name, [NotNull] [ItemNotNull] IEnumerable<LaundryMachineVault> laundryMachines, [NotNull] [ItemNotNull] IEnumerable<ILaundryRepository> laundryRepositories)
        {
            if (laundryMachines == null) throw new ArgumentNullException(nameof(laundryMachines));
            if (laundryRepositories == null) throw new ArgumentNullException(nameof(laundryRepositories));

            var tempRep = laundryRepositories.ToArray();
            var tempArr = laundryMachines.ToArray();
            if (tempArr.Any(itm => itm == null))
                throw new ArgumentException(@"One or more laundry machines was null.", nameof(laundryMachines));
            if (tempRep.Any(itm => itm == null))
                throw new ArgumentException(@"One or more laundry repositories was null.", nameof(laundryRepositories));
            Repositories = tempRep.ToImmutableArray();
            Machines = tempArr.ToImmutableArray();
            _robotName = name ?? throw new ArgumentNullException(nameof(name));
            RobotId = Guid.NewGuid();
            _concreteType = new LocklessConcreteType(this);
            _eventRaisingThread = EventRaisingThread.CreateEventRaiser($"RobotEvent{name}");
            _robotThread = new Thread(RobotThread) {Name = name, IsBackground = true, Priority = ThreadPriority.Lowest};
            
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                if (State != RobotState.ThreadTerminated)
                {
                    try
                    {
                        _cts.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync(ex.ToString());
                    }
                }

                try
                {
                    _eventRaisingThread.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }

                RobotActed = null;
            }
            _disposed.TrySet();
        }

        protected void ThrowIfDisposed([CallerMemberName] string caller = "")
        {
            if (_disposed.IsSet)
            {
                throw new ObjectDisposedException(ConcreteTypeName,
                    $"Illegal call to {_robotName}'s {caller ?? "UNKNOWN"} member: the object is disposed.");
            }
        }


        protected abstract void ExecuteRobotJob(in CancellationToken token);
        

        private void PerformRobotActions(CancellationToken token)
        {
            if (_activityFlag.SetFromPausingToPaused())
            {
                DoPause(token);
            }
            else
            {
                ExecuteRobotJob(token);
            }
        }

        

        private void RobotThread(object tokenObj)
        {
            try
            {
                if (_activityFlag.SetFromStartingUpToPaused() && tokenObj is CancellationToken token)
                {
                    try
                    {
                        DoPause(token);
                        while (true)
                        {
                            try
                            {
                                PerformRobotActions(token);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                OnRobotActivity(TimeStampSource.Now, $"I encountered an error: [{ex}]");
                                throw;
                            }

                        }
                    }
                    catch (OperationCanceledException)
                    {
                        OnRobotActivity(TimeStampSource.Now, "I am shutting down.");
                    }
                }
                else
                {
                    OnRobotActivity(TimeStampSource.Now, "I couldn't even start up.");
                }
            }
            finally
            {
                _activityFlag.ForceTerminated();
            }
        }

        protected virtual void DoPause(CancellationToken token)
        {
            if (_activityFlag.State == RobotState.Paused || _activityFlag.SetFromPausingToPaused())
            {
                OnRobotActivity(TimeStampSource.Now, "I entered the PAUSED state.");
                while (_activityFlag.State == RobotState.Paused)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(50));
                    token.ThrowIfCancellationRequested();
                }
            }
            else
            {
                TerminationHelper.TerminateApplication($"Robot {_robotName} unable ato set state to paused");
            }
        }

        protected virtual void OnRobotActivity(DateTime ts, string action)
        {
            if (action != null && RobotActed != null)
            {
                _eventRaisingThread.AddAction(() =>
                {
                    RobotActedEventArgs args = new RobotActedEventArgs(ts, action, this);
                    try
                    {
                        RobotActed?.Invoke(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync(ex.ToString());
                    }
                });
            }
        }

        private static readonly TimeSpan TheMaxTimeToStart = TimeSpan.FromMilliseconds(750);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Thread _robotThread;
        [NotNull] private readonly string _robotName;
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        [NotNull] protected readonly BasicVault<LaundryItems?> _heldLaundryItem =
            new BasicVault<LaundryItems?>(null, TimeSpan.FromMilliseconds(250));
        protected RobotActivityFlag _activityFlag = new RobotActivityFlag();
        [NotNull] private readonly LocklessConcreteType _concreteType;
        [NotNull] private readonly IEventRaiser _eventRaisingThread;
        
    }
}