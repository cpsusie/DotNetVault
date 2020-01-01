using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DotNetVault.Attributes;
using JetBrains.Annotations;
using LaundryMachine.LaundryCode;
using LaundryMachineModel = LaundryMachine.LaundryCode.ILaundryMachine;
namespace LaundryMachineUi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            MyDispatcher = new LocklessLazyWriteOnce<Dispatcher>(() => Dispatcher.CurrentDispatcher);
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
               pnlMachine1.Dispose();
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        private Dispatcher MyDispatcher { get; set; }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            (LaundryMachineVault MachineVault, IPublishLaundryMachineEvents EventPublisher)? vaultPair = null;
            try
            {
                MyDispatcher = Dispatcher.FromThread(Thread.CurrentThread);
                vaultPair = LaundryMachineVault.CreateVaultAndEventPublisher(TimeSpan.FromSeconds(2),
                    TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150));
                //lm = LaundryMachineFactorySource.FactoryInstance(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150));
                pnlMachine1.SupplyLaundryMachine(vaultPair.Value.MachineVault, vaultPair.Value.EventPublisher);
            }
            catch (Exception ex)
            {
                pnlMachine1?.Dispose();
                vaultPair?.MachineVault?.Dispose();
                vaultPair?.EventPublisher?.Dispose();
                MessageBox.Show($"Unexpected exception creating model.  Content: [{ex}].", "Error",
                    MessageBoxButton.OK,MessageBoxImage.Error);
                Environment.Exit(-1);
            }
        }
        

        private void PostAction([NotNull] Action a)
        {
            if (MyDispatcher.CheckAccess())
            {
                a();
            }
            else
            {
                MyDispatcher.InvokeAsync(a).FireAndForget();
            }
        }
        private async void btnLoadAndCycle_Click(object sender, RoutedEventArgs e)
        {
            bool setLoadPending = _loadPending.TrySet();
            if (!setLoadPending) return;
            try
            {
                if (_currentDirty == LaundryItems.InvalidItem)
                {
                    if (_dirtyLaundry.Any())
                    {
                        _currentDirty = _dirtyLaundry.Dequeue();
                        Debug.Assert(_currentDirty != LaundryItems.InvalidItem);
                    }
                    else
                    {
                        MessageBox.Show("No dirty laundry available.", "No dirty laundry", MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                }

                var res = await pnlMachine1.LoadAndCycleAsync(_currentDirty);
                if (res.LoadedLaundry != null)
                {
                    _reclamationId = res.LoadedLaundry.Value;
                    _currentDirty = LaundryItems.InvalidItem;
                    if (!res.Cycled)
                    {
                        PostAction(() => MessageBox.Show("Laundry loaded but unable to cycle.", "Unable To Cycle", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                }
                else
                {
                    PostAction(() => MessageBox.Show("Unable to load and cycle laundry.  Try again later",
                        "Unable to Load", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            }
            finally
            {
                _loadPending.TryClear();
            }
        }
        private async void btnLoadDirty_Click(object sender, RoutedEventArgs e)
        {
            bool setLoadPending = _loadPending.TrySet();
            if (!setLoadPending) return;

            try
            {
                if (_currentDirty == LaundryItems.InvalidItem)
                {
                    if (_dirtyLaundry.Any())
                    {
                        _currentDirty = _dirtyLaundry.Dequeue();
                        Debug.Assert(_currentDirty != LaundryItems.InvalidItem);
                    }
                    else
                    {
                        MessageBox.Show("No dirty laundry available.", "No dirty laundry", MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                }

                Guid? temp = await pnlMachine1.LoadLaundryAsync(in _currentDirty);
                if (temp != null)
                {
                    _reclamationId = temp.Value;
                    _currentDirty = LaundryItems.InvalidItem;
                }
            }
            finally
            {
                _loadPending.TryClear();
            }
        }
        
        private async void btnUnload_Click(object sender, RoutedEventArgs e)
        {
            LaundryItems? item = null;
            bool setIt = false;
            Guid? id = _reclamationId;
            if (id != null)
            {
                try
                {
                    
                    setIt = _unloadPending.TrySet();
                    if (setIt)
                    {
                        item = await pnlMachine1.UnloadAnyLaundryAsync();
                        if (item != null)
                        {
                            
                            var targetQueue = item.Value.SoiledFactor == 0 ? _cleanLaundry : _dirtyLaundry;
                            Debug.Assert(targetQueue != null);
                            targetQueue.Enqueue(item.Value);
                        }
                    }
                }
                finally
                {
                    if (setIt)
                    {
                        _loadPending.TryClear();
                        if (item != null)
                        {
                            Debug.Assert(_reclamationId == null || item.Value.ItemId == _reclamationId);
                            if (item.Value.ItemId == _reclamationId)
                            {
                                _reclamationId = null;
                            }
                            _reclamationId = null;
                        }
                    }
                }
            }
        }

        private LocklessToggleFlag _unloadPending = default;
        private LocklessToggleFlag _loadPending = default;
        private Guid? _reclamationId; 
        private LaundryItems _currentDirty;
        private readonly Queue<LaundryItems> _cleanLaundry = new Queue<LaundryItems>();        

        private readonly Queue<LaundryItems> _dirtyLaundry = new Queue<LaundryItems>(new[]
        {
            LaundryItems.CreateLaundryItems("Liturgical Vestments", 0.25m, 175, 0),
            LaundryItems.CreateLaundryItems("Undergarments", 0.035m, 255, 8),
            LaundryItems.CreateLaundryItems("Bed sheets", 0.37m, 192, 1),
        });

      
    }

    internal struct LocklessToggleFlag
    {
        public bool IsSet
        {
            get
            {
                int val = _value;
                return val != Clear;
            }
        }

        public bool TrySet()
        {
            const int wantToBe = Set;
            const int needToBeNow = Clear;
            return Interlocked.CompareExchange(ref _value, wantToBe, needToBeNow) == needToBeNow;
        }

        public bool TryClear()
        {
            const int wantToBe = Clear;
            const int needToBeNow = Set;
            return Interlocked.CompareExchange(ref _value, wantToBe, needToBeNow) == needToBeNow;
        }

        public override string ToString() => $"{nameof(LocklessToggleFlag)} -- {(IsSet ? "SET" : "CLEAR")}";

        private const int Clear = 0;
        private const int Set = 0;
        private volatile int _value;
    }

    public static class Experiment
    {
        [return: UsingMandatory]
        public static StreamReader GetStreamReader(string path) => new StreamReader(path);
    }

    public static class DispatcherOperationExtension
    {
        public static void FireAndForget([NotNull] this DispatcherOperation op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
        }

        public static void FireAndForget<T>([NotNull] this DispatcherOperation<T> op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
        }
    }
}
