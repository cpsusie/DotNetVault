using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JetBrains.Annotations;
using LaundryMachine.LaundryCode;

namespace LaundryMachineUi
{
    public delegate bool BooleanLaundryCommand(in LockedLaundryMachine llm);

    /// <summary>
    /// Interaction logic for LaundryMachineControlPanelView.xaml
    /// </summary>
    public partial class LaundryMachineControlPanelView : ILaundryMachineControlPanelView
    {
        public bool ShowCommandButtons
        {
            get => gpbxLaundryCommands.Visibility != Visibility.Visible;
            set => PostAction(() =>
            {
                if ((gpbxLaundryCommands.Visibility == Visibility.Visible) != value)
                {
                    gpbxLaundryCommands.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            });
        }

        public bool IsDisposed => _disposed.IsSet;
        public long LaundryMachineNumber { get; private set; }
        public LaundryMachineStateCode StateCode
        {
            get => _code;
            private set
            {
                _dispatcher.VerifyAccess();
                if (value != _code)
                {
                    _code = value;
                    txbxStateCode.Text = value.ToString();
                    UpdateButtons(value);
                }

                void UpdateButtons(LaundryMachineStateCode val)
                {
                     btnTurnOn.IsEnabled = val == LaundryMachineStateCode.PoweredDown;
                     btnTurnOff.IsEnabled = !btnTurnOn.IsEnabled;
                     btnWash.IsEnabled = val == LaundryMachineStateCode.Full;
                     btnDry.IsEnabled = val == LaundryMachineStateCode.Full;
                     btnAbort.IsEnabled =
                         val == LaundryMachineStateCode.Washing || val == LaundryMachineStateCode.Drying;
                }
            }
        }

        public void RefreshStateCode()
        {
            VerifyAccess();
            string txt = _code.ToString();
            txbxStateCode.Text = txt;
        }

        public LaundryMachineControlPanelView()
        {
            InitializeComponent();
            _dispatcher = Dispatcher.CurrentDispatcher;
            if (_dispatcher == null) throw new InvalidOperationException("Could not verify dispatcher presence.");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            VerifyAccess();
            if (disposing && _disposed.TrySet())
            {
                LaundryMachineVault lmv = _laundryMachine;
                IPublishLaundryMachineEvents pe = _eventPublisher;
                pe?.Dispose();
                if (lmv != null)
                {
                    {
                        using var lck = lmv.SpinLock(TimeSpan.FromSeconds(2));
                        lck.DisposeLaundryMachine();
                    }
                    lmv.Dispose();
                }

                _laundryMachine = null;
                _eventPublisher = null;
            }
            _disposed.TrySet();
        }

        public void SupplyLaundryMachine(LaundryMachineVault lm, IPublishLaundryMachineEvents eventPublisher)
        {
            if (lm == null) throw new ArgumentNullException(nameof(lm));
            if (eventPublisher == null) throw new ArgumentNullException(nameof(eventPublisher));
            PostAction(Action);
            void Action()
            {
                try
                {
                    
                    LaundryMachineVault mv = _laundryMachine;
                    IPublishLaundryMachineEvents pe = _eventPublisher;
                    pe?.Dispose();
                    mv?.Dispose();
                    _laundryMachine = lm;
                    _eventPublisher = eventPublisher;

                    LaundryMachineStateCode code;
                    long machineNumber;

                    using (var lck = lm.SpinLock(TimeSpan.FromSeconds(1)))
                    {
                        code = lck.StateCode;
                        machineNumber = lck.MachineId;
                    }

                    LaundryMachineNumber = machineNumber;
                    gpbxMonitor.Header = $"Laundry Machine Status Monitor (#{machineNumber})";
                    gpbxLaundryCommands.Header = $"Command Panel (#{machineNumber})";
                    StateCode = code;
                    txbxStateCode.Text = code.ToString();
                    txbxContents.Text = "EMPTY";
                    eventPublisher.LaundryLoadedOrUnloaded += _model_LaundryLoadedOrUnloaded;
                    eventPublisher.LaundryMachineChangedState += _model_LaundryMachineChangedState;
                    eventPublisher.MachineStatusUpdated += _model_MachineStatusUpdated;
                    eventPublisher.Terminated += _model_Terminated;
                    eventPublisher.AccessToLaundryMachineTimedOut += _model_AccessToLaundryMachineTimedOut;
                    IsEnabled = true;
                    
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unexpected exception creating model.  Content: [{ex}].", "Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Environment.Exit(-1);
                }
            }
        }

       
        public (bool Success, LaundryMachineVault ReleasedMachine, IPublishLaundryMachineEvents EventPublisher) TryReleaseMachine()
        {
            return _dispatcher.CheckAccess() ? ReleaseAction() : _dispatcher.Invoke(ReleaseAction);


            (bool Success, LaundryMachineVault ReleasedMachine, IPublishLaundryMachineEvents EventPublisher) ReleaseAction()
            {
                bool success;
                LaundryMachineVault v;
                IPublishLaundryMachineEvents pe;

                if (_laundryMachine == null || _eventPublisher == null)
                {
                    success = false;
                    v = null;
                    pe = null;
                }
                else
                {
                    v = _laundryMachine;
                    pe = _eventPublisher;
                    pe.LaundryMachineChangedState -= _model_LaundryMachineChangedState;
                    pe.MachineStatusUpdated -= _model_MachineStatusUpdated;
                    pe.Terminated -= _model_Terminated;
                    pe.AccessToLaundryMachineTimedOut -= _model_AccessToLaundryMachineTimedOut;
                    IsEnabled = false;
                    StateCode = LaundryMachineStateCode.Error;
                    rtbxEventLog.Document.Blocks.Clear();
                    _laundryMachine = null;
                    _eventPublisher = null;
                    LaundryMachineNumber = 0;
                    success = v != null && pe != null;
                }
                return (success, v, pe);
            }
        }


        public Task<LaundryItems?> UnloadLaundryAsync(in Guid id)
        {
            Guid copy = id;
            return Task.Run(() => DoUnloading(copy));
        }

        public Task<LaundryItems?> UnloadAnyLaundryAsync() => Task.Run(DoUnloadAny);
        
        public Task<Guid?> LoadLaundryAsync(in LaundryItems items)
        {
            LaundryItems copy = items;
            return Task.Run(() => DoLoading(copy));
        }

        public Task<(Guid? LoadedLaundry, bool Cycled)> LoadAndCycleAsync(in LaundryItems item)
        {
            LaundryItems copy = item;
            return Task.Run(() => DoLoadAndCycle(copy));
        }

        public Task<bool> ExecuteAbortAsync() => Task.Run(DoAbort);

        public Task<bool> ExecuteWashDryAsync() => Task.Run(DoExecuteWashDry);

        public Task<bool> ExecuteDryAsync() => Task.Run(DoExecuteDry);

        private bool DoExecuteWashDry()
        {
            bool ret = false;
            LaundryMachineVault v = _laundryMachine;
            if (v != null)
            {
                try
                {
                    using var lck = v.SpinLock(TimeSpan.FromSeconds(2));
                    ret = lck.InitiateWashDry();
                }
                catch (TimeoutException)
                {
                    ret = false;
                }
            }
            return ret;
        }

        private bool DoExecuteDry()
        {
            bool ret = false;
            LaundryMachineVault v = _laundryMachine;
            if (v != null)
            {
                try
                {
                    using var lck = v.SpinLock(TimeSpan.FromSeconds(2));
                    ret = lck.InitiateDry();
                }
                catch (TimeoutException)
                {
                    ret = false;
                }
            }
            return ret;
        }

        private Guid? DoLoading(LaundryItems item)
        {
            Guid? ret;
            var vault = _laundryMachine;
            string description = item != LaundryItems.InvalidItem ? item.ItemDescription : string.Empty;
            if (vault != null && item != LaundryItems.InvalidItem)
            {
                try
                {
                    
                    using var lck = vault.SpinLock(TimeSpan.FromSeconds(2));
                    ret = lck.LoadLaundry(in item);
                }
                catch (TimeoutException)
                {
                    ret = null;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    ret = null;
                }
            }
            else
            {
                ret = null;
            }
            if (ret != null)
            {
                _dispatcher.InvokeAsync(() => txbxContents.Text = description);
            }
            return ret;
        }
        private LaundryItems? DoUnloading(Guid id)
        {
            LaundryItems? ret;
            var vault = _laundryMachine;
            if (vault != null)
            {
                try
                {
                    using var lck = vault.SpinLock(TimeSpan.FromSeconds(2));
                    ret = lck.UnloadLaundry(in id);
                }
                catch (TimeoutException)
                {
                    ret = null;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    ret = null;
                }
            }
            else
            {
                ret = null;
            }

            if (ret != null)
            {
                _dispatcher.InvokeAsync(() => txbxContents.Text = "EMPTY");
            }
            return ret;
        }

        private LaundryItems? DoUnloadAny()
        {
            LaundryItems? ret;
            var vault = _laundryMachine;
            if (vault != null)
            {
                try
                {
                    using var lck = vault.SpinLock(TimeSpan.FromSeconds(2));
                    ret = lck.UnloadAnyLaundry();
                }
                catch (TimeoutException)
                {
                    ret = null;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    ret = null;
                }

                
            }
            else
            {
                ret = null;
            }

            if (ret != null)
            {
                _dispatcher.InvokeAsync(() => txbxContents.Text = "EMPTY");
            }
            return ret;
        }

        private (Guid? LaundryId, bool Cycled) DoLoadAndCycle(LaundryItems item)
        {
            (Guid? LaundryId, bool Cycled) ret = (null, false);
            var vault = _laundryMachine;
            if (vault != null && item != LaundryItems.InvalidItem)
            {
                try
                {
                    using var lck = vault.SpinLock(TimeSpan.FromSeconds(2));
                    ret = lck.LoadAndCycle(in item);
                }
                catch (TimeoutException)
                {
                    //ignore
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
            }

            if (ret.LaundryId != null)
            {
                _dispatcher.InvokeAsync(() => txbxContents.Text = item.ItemDescription);
            }

            return ret;
        }

        private bool DoAbort()
        {
            bool ret;
            var vault = _laundryMachine;
            if (vault != null)
            {
                try
                {
                    using var lck = vault.SpinLock(TimeSpan.FromSeconds(2));
                    ret = lck.Abort();
                }
                catch (TimeoutException)
                {
                    ret = false;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    ret = false;
                }
            }
            else
            {
                ret = false;
            }
            return ret;
        }

        private void _model_Terminated(object sender, EventArgs e) => PostAction(delegate
        {
            LogEvent($"At [{TimeStampSource.Now:O}], the state machine terminated.");
        });

        
        private void _model_MachineStatusUpdated(object sender, LaundryMachineStatusEventArgs e)
            => PostAction(delegate
            {
                LogEvent($"At [{TimeStampSource.Now:O}] received following update event:\t\t\t[{e}]");
            });

        private void
            _model_LaundryMachineChangedState(object sender, StateChangedEventArgs<LaundryMachineStateCode> e) =>
            PostAction(delegate
            {
                StateCode = e.NewState;
                LogEvent($"At [{TimeStampSource.Now:O}], state changed from [{e.OldState}] to [{e.NewState}]");
            });

        private void _model_AccessToLaundryMachineTimedOut(object sender, LaundryMachineAccessTimeoutEventArgs e)
            => PostAction(delegate
            {
                if (e != null)
                {
                    LogEvent(e.ToString());
                }
            });

        private void _model_LaundryLoadedOrUnloaded(object sender, LaundryLoadedUnloadEventArgs e)
            => PostAction(delegate
            {
                if (e != null)
                {
                    if (e.Loaded)
                    {
                        txbxContents.Text = e.LaundryDescription;
                    }
                    else
                    {
                        txbxContents.Text = "EMPTY";
                    }
                    LogEvent(e.ToString());
                }
            });


        private void LogEvent(string logMe)
        {

            rtbxEventLog.AppendText(logMe + Environment.NewLine);
            rtbxEventLog.ScrollToEnd();
        }

        private void btnTurnOn_Clicked(object sender, RoutedEventArgs e)
            =>
                ProcessBooleanCommand((in LockedLaundryMachine mach) => mach.TurnOnMachine(), nameof(ILaundryMachine.TurnOnMachine));

        private void btnTurnOff_Clicked(object sender, RoutedEventArgs e) =>
            ProcessBooleanCommand((in LockedLaundryMachine mach) => mach.TurnOffMachine(), nameof(ILaundryMachine.TurnOffMachine));

        private async void btnWash_Clicked(object sender, RoutedEventArgs e)
        {
            bool setIt = _washDryAbortPendingFlag.TrySet();
            if (setIt)
            {
                try
                {
                    bool ok = await ExecuteWashDryAsync();
                    if (!ok)
                    {
                        ShowErrorMessage();
                    }
                }
                finally
                {
                    _washDryAbortPendingFlag.TryClear();
                }
            }
            else
            {
                ShowErrorMessage();
            }

            void ShowErrorMessage() =>
                PostAction(() => MessageBox.Show(
                    "The wash command cannot be executed at present or another wash/dry/abort command is still being processed.",
                    "Command Rejected",
                    MessageBoxButton.OK, MessageBoxImage.Information));
        }
        private async void btnDry_Clicked(object sender, RoutedEventArgs e)
        {
            bool setIt = _washDryAbortPendingFlag.TrySet();
            if (setIt)
            {
                try
                {
                    bool ok = await ExecuteDryAsync();
                    if (!ok)
                    {
                        ShowErrorMessage();
                    }
                }
                finally
                {
                    _washDryAbortPendingFlag.TryClear();
                }
            }
            else
            {
                ShowErrorMessage();
            }

            void ShowErrorMessage() =>
                PostAction(() => MessageBox.Show(
                    "The dry command cannot be executed at present or another wash/dry/abort command is still being processed.",
                    "Command Rejected",
                    MessageBoxButton.OK, MessageBoxImage.Information));
        }
        private async void btnAbort_Clicked(object sender, RoutedEventArgs e)
        {
            bool ok = await ExecuteAbortAsync();
            if (!ok)
            {
                PostAction(() => MessageBox.Show("The abort command could not be processed.  Try again later.",
                    "Abort Rejected", MessageBoxButton.OK, MessageBoxImage.Information));
            }
        }

        private void ProcessBooleanCommand([NotNull] BooleanLaundryCommand command, [NotNull] string commandName)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (commandName == null) throw new ArgumentNullException(nameof(commandName));
            _dispatcher.VerifyAccess();
            var vault = _laundryMachine;
            if (vault != null)
            {
                try
                {
                    using var lck = vault.SpinLock(TimeSpan.FromSeconds(2));
                    bool ret = command(in lck);
                    if (!ret)
                    {
                        MessageBox.Show($"The machine rejected the [{commandName}] command.  Try again later.",
                            "Command Rejected", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (TimeoutException)
                {
                    MessageBox.Show($"The machine is busy.  Try the {commandName} command again later.", "Timeout",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"The machine threw an exception when attempting to process the [{command}] command. Exception contents: [{ex}]." +
                        $"  The application will now terminate.", "FAULTED",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(-1);
                }
            }
            else
            {
                Console.Error.WriteLineAsync("No machine exists to process the command.");
            }
        }

        //private void ShowNotImplemented() => MessageBox.Show("This functionality is not yet implemented.",
        //    "Not Implemented", MessageBoxButton.OK,
        //    MessageBoxImage.Information);

        private void PostAction([NotNull] Action a)
        {
            if (_dispatcher.CheckAccess())
            {
                a();
            }
            else
            {
               _dispatcher.InvokeAsync(a).FireAndForget();
            }
        }

        private LocklessToggleFlag _washDryAbortPendingFlag = new LocklessToggleFlag();
        private LocklessSetOnceFlagVal  _disposed = new LocklessSetOnceFlagVal();
        private LaundryMachineStateCode _code;
        private readonly Dispatcher _dispatcher;
        [CanBeNull] private LaundryMachineVault _laundryMachine;
        [CanBeNull] private IPublishLaundryMachineEvents _eventPublisher;
            
        
    }
}
