using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using LaundryStatusVault = LaundryMachine.LaundryCode.LaundryStatusFlagVault;
namespace LaundryMachine.LaundryCode
{
    public sealed class LaundryStateMachine : ILaundryStateMachine
    {
        public event EventHandler Terminated;
        public event EventHandler Disposed;
        public event EventHandler<UnexpectedStateMachineFaultEventArgs> UnexpectedExceptionThrown;
        public event EventHandler<StateChangedEventArgs<LaundryMachineStateCode>> StateChanged;
        public event EventHandler<TransitionPredicateTrueEventArgs<LaundryMachineStateCode>> TransitionPredicateTrue;

        public ulong StateChangeCount => _stateChangeCount.Value;
        public bool IsDisposed => _disposed.IsSet;
        public bool StateThreadActive => _threadStatusFlag.Code == ThreadStatusFlagCode.ThreadStarted;
        public bool EventThreadActive => !_eventRaisingThread.IsDisposed && _eventRaisingThread.ThreadActive;
        public bool StartMachineEverCalled => _threadStatusFlag.Code >= ThreadStatusFlagCode.RequestedThreadStart;
        public BasicVault<LaundryMachineStateCode> StateVault { get; } =
            new BasicVault<LaundryMachineStateCode>(default, TimeSpan.FromSeconds(2));
        public LaundryStatusVault FlagVault { get; } =
            new LaundryStatusVault(TimeSpan.FromSeconds(2), () => new LaundryStatusFlags());

        
        internal LaundryStateMachine(TimeSpan? delayAtEndOfLoop, int maxNoYield, TimeSpan timeToAddOneUnitOfDamp, TimeSpan timeToRemoveOneUnitOfSoil, TimeSpan timeToRemoveOneUnitOfDamp)
        {
            if (maxNoYield < 1) throw new ArgumentOutOfRangeException(nameof(maxNoYield), maxNoYield, @"Parameter must be positive.");
            if (delayAtEndOfLoop.HasValue && delayAtEndOfLoop <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delayAtEndOfLoop),
                    @"Value must be null or positive, not negative or zero.");
            long id = Interlocked.Increment(ref _sId);
            _maxNoYield = maxNoYield;
            _delayEndOfLoop = delayAtEndOfLoop;
            _timeToAddOneUnitDampness = timeToAddOneUnitOfDamp;
            _timeToRemoveOneUnitDirt = timeToRemoveOneUnitOfSoil;
            _timeToRemoveOneUnitDampness = timeToRemoveOneUnitOfDamp;
            _eventRaisingThread = EventRaisingThread.CreateEventRaiser($"LsmEvntRsr{id}");
            if (_threadStatusFlag.TrySetInstantiated() )
            {
                _stateMachineThread = new Thread(ThreadLoop);
                _stateMachineThread.IsBackground = true;
                _stateMachineThread.Priority = ThreadPriority.BelowNormal;
                _stateMachineThread.Name = $"LaundryStateMachine{id}";
            }
            else
            {
                Console.Error.WriteLineAsync($"Logic error in {nameof(LaundryStateMachine)}'s CTOR.");
                Environment.Exit(-1);
                throw new StateLogicErrorException("Couldn't start it up");
            }
        }

        public void Dispose() => Dispose(true);

        public void StartStateMachine()
        {
            if (_threadStatusFlag.TrySetRequestedThreadStart())
            {
                _stateMachineThread.Start(_cts.Token);
                DateTime quitAfter = TimeStampSource.Now + TimeSpan.FromSeconds(2);
                while (_threadStatusFlag.Code == ThreadStatusFlagCode.RequestedThreadStart &&
                       TimeStampSource.Now <= quitAfter)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }


                if (_threadStatusFlag.Code != ThreadStatusFlagCode.ThreadStarted)
                {
                    try
                    {
                        Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync(ex.ToString());
                        Debug.WriteLine(ex.ToString());
                        Environment.Exit(-1);
                    }

                    throw new InvalidOperationException(
                        "Unable to confirm state machine active status within reasonable period.");
                }
            }
            else
            {
                throw new InvalidOperationException("The state machine has already been started.");
            }
        }

        private void ThreadLoop(object cancellationTokenObject)
        {
            if (cancellationTokenObject is CancellationToken token && _threadStatusFlag.TrySetThreadStarted())
            {
                try
                {
                    _currentState = SetupFirstState();

                    int noYieldCount = 0;
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        (LaundryMachineStateCode? NextStateCode,
                            LaundryStateMachineState NextState) result =
                                _currentState.FindAndExecutePossibleTransition(TimeSpan.FromSeconds(5));
                        if (result.NextStateCode.HasValue && result.NextStateCode != _currentState.StateCode)
                        {
                            _currentState.EstablishExitInvariants();
                            _currentState.Dispose();
                            SetupNextState(result.NextState, token);
                            noYieldCount = 0; //reset no yields when an iteration of the loop does something potentially substantive.
                        }

                        token.ThrowIfCancellationRequested();
                        if (!Thread.Yield())
                        {
                            if (_delayEndOfLoop != null)
                            {
                                if (++noYieldCount > _maxNoYield)
                                {
                                    token.ThrowIfCancellationRequested();
                                    Thread.Sleep(_delayEndOfLoop.Value);
                                    noYieldCount = 0;
                                }
                            }
                        }
                        else
                        {
                            noYieldCount = 0;
                        }
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    try
                    {
                        OnUnexpectedExceptionThrown(new UnexpectedStateMachineFaultEventArgs(TimeStampSource.Now, e));
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLineAsync(exception.ToString());
                        Debug.WriteLine(exception.ToString());
                        throw;
                    }

                }
                finally
                {
                    try
                    {
                        var currentState = _currentState;
                        if (currentState?.IsDisposed == false)
                        {
                            currentState?.Dispose();
                        }

                        _currentState = null;

                        if (_threadStatusFlag.TrySetThreadTerminated())
                        {
                            try
                            {
                                OnTerminated();
                            }
                            catch (Exception)
                            {
                                //ignored
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync(ex.ToString());
                        Debug.WriteLine(ex.ToString());
                    }
                }
            }
            else
            {
                if (_threadStatusFlag.TrySetThreadTerminated())
                {
                    try
                    {
                        OnTerminated();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync(ex.ToString());
                        Debug.WriteLine(ex.ToString());
                    }
                }
                else
                {
                    _threadStatusFlag.ForceTerminate();
                }
            }
        }

        private LaundryStateMachineState SetupFirstState() => new PoweredDownState(FlagVault, StateVault,
            _eventRaisingThread, _taskContext, _timeToAddOneUnitDampness, _timeToRemoveOneUnitDirt,
            _timeToRemoveOneUnitDampness);

        private void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                var currentState = _currentState;
                try
                {
                    currentState?.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }
                _currentState = null;
                
                if (_threadStatusFlag.Code != ThreadStatusFlagCode.ThreadTerminated)
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
                    _cts.Dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }

                DateTime waitABit = TimeStampSource.Now + TimeSpan.FromSeconds(10);
                while (_threadStatusFlag.Code != ThreadStatusFlagCode.ThreadTerminated &&
                       TimeStampSource.Now <= waitABit)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }

                if (_threadStatusFlag.Code != ThreadStatusFlagCode.ThreadTerminated)
                {
                    const string noStopSm = "Unable to stop state machine thread in ten seconds.";
                    Console.Error.WriteLineAsync(noStopSm);
                    Debug.WriteLine(noStopSm);
                }

                if (!StateVault.TryDispose(TimeSpan.FromSeconds(10)))
                {
                    string noDisposeStateCodeVaultTenSecs = "Couldn't dispose state code vault in ten seconds.";
                    Console.Error.WriteLineAsync(noDisposeStateCodeVaultTenSecs);
                    Debug.WriteLine(noDisposeStateCodeVaultTenSecs);
                }

                if (!FlagVault.TryDispose(TimeSpan.FromSeconds(10)))
                {
                    string noDisposeStateCodeVaultTenSecs = "Couldn't dispose status flags vault in ten seconds.";
                    Console.Error.WriteLineAsync(noDisposeStateCodeVaultTenSecs);
                    Debug.WriteLine(noDisposeStateCodeVaultTenSecs);
                }

                try
                {
                    _eventRaisingThread.Dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }

                try
                {
                    _taskContext?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(10));

                OnDisposed();

                Terminated = null;
                Disposed = null;
                UnexpectedExceptionThrown = null;
                StateChanged = null;
                TransitionPredicateTrue = null;
            }
        }

        private void SetupNextState(LaundryStateMachineState resultNextState, CancellationToken token)
        {
            try
            {
                resultNextState.TransitionPredicateTrue += ResultNextState_TransitionPredicateTrue;
                LaundryMachineStateCode oldState, newState;
                ulong count;
                using (var lck = StateVault.SpinLock(token))
                {
                    _currentState = resultNextState;
                    resultNextState.ValidateEntryInvariants();
                    resultNextState.Begin();
                    oldState = lck.Value;
                    newState = resultNextState.StateCode;
                    count = (oldState != newState) ? _stateChangeCount.Increment() : _stateChangeCount.Value;
                    lck.Value = newState;
                }
                
                OnStateChanged(new StateChangedEventArgs<LaundryMachineStateCode>(oldState, newState, count));
            }
            catch (Exception e)
            {
                OnUnexpectedExceptionThrown(new UnexpectedStateMachineFaultEventArgs(TimeStampSource.Now, e));
                SetupError(e, "Error setting up state.");
            }
        }
        
        

        private void SetupError(Exception ex, string message)
        {
            string exM = ex?.ToString() ?? "NULL";
            Console.Error.WriteLineAsync(exM);
            Console.Error.WriteLineAsync(message);
            Debug.WriteLine(exM);
            try
            {
                Dispose();
            }
            catch (Exception ex2)
            {
                string ex2M = ex2.ToString();
                Console.Error.WriteLineAsync(ex2M);
                Debug.WriteLine(ex2M);
            }
            Environment.Exit(-1);
        }

        private void OnTransitionPredicateTrue(TransitionPredicateTrueEventArgs<LaundryMachineStateCode> e) =>
            RaiseEvent(TransitionPredicateTrue, e);
        private void OnStateChanged(StateChangedEventArgs<LaundryMachineStateCode> e) => RaiseEvent(StateChanged, e);
        private void OnTerminated() => RaiseEvent(Terminated);
        private void OnUnexpectedExceptionThrown(UnexpectedStateMachineFaultEventArgs e)
            => RaiseEvent(UnexpectedExceptionThrown, e);
        private void OnDisposed()
        {
            if (_eventRaisingThread.IsDisposed || !_eventRaisingThread.ThreadActive)
            {
                Action();
            }
            else
            {
                _eventRaisingThread.AddAction(Action);
            }
            void Action() => Disposed?.Invoke(this, EventArgs.Empty);
        }
        private void RaiseEvent<TEventArgs>(EventHandler<TEventArgs> handler, TEventArgs args)
            where TEventArgs : EventArgs
        {
            if (_eventRaisingThread.ThreadActive && !_eventRaisingThread.IsDisposed && args != null)
            {
                try
                {
                    _eventRaisingThread.AddAction(Action);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }
            }

            void Action() => handler?.Invoke(this, args);
        }

        private void RaiseEvent(EventHandler handler)
        {
            if (_eventRaisingThread.ThreadActive && !_eventRaisingThread.IsDisposed)
            {
                try
                {
                    _eventRaisingThread.AddAction(Action);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }
            }

            void Action() => handler?.Invoke(this, EventArgs.Empty);
        }

        private void ResultNextState_TransitionPredicateTrue(object sender,
            TransitionPredicateTrueEventArgs<LaundryMachineStateCode> e) => OnTransitionPredicateTrue(e);
        



        //Only accessed from thread loop
        private LaundryStateMachineState _currentState;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly LocklessSetOnlyFlag _disposed = new LocklessSetOnlyFlag();
        private ThreadStatusFlag _threadStatusFlag = new ThreadStatusFlag();
        [NotNull] private readonly IEventRaiser _eventRaisingThread;
        private readonly ILaundryMachineTaskExecutionContext<TaskResult> _taskContext =
            LaundryMachineTaskExecutionContext.CreateExecutionContext();
        [NotNull] private readonly Thread _stateMachineThread;
        private readonly TimeSpan? _delayEndOfLoop;
        private readonly int _maxNoYield;
        private readonly TimeSpan _timeToAddOneUnitDampness;
        private readonly TimeSpan _timeToRemoveOneUnitDirt;
        private readonly TimeSpan _timeToRemoveOneUnitDampness;
        [NotNull] private readonly ThreadSafeCounter _stateChangeCount = new ThreadSafeCounter(); 
        private static long _sId;

        
    }

    public sealed class ThreadSafeCounter
    {
        public static implicit operator ulong([NotNull] ThreadSafeCounter tsc)
        {
            if (tsc == null) throw new ArgumentNullException(nameof(tsc));
            return tsc._count;
        }

        public ulong Value
        {
            get
            {
                lock (_syncObject) return _count;
            }
        }

        public ulong Increment()
        {
            ulong ret;
            lock (_syncObject)
            {
                ret = ++_count;
            }
            return ret;
        }


        private ulong _count = 0;
        private readonly object _syncObject = new object();
    }
 

}
