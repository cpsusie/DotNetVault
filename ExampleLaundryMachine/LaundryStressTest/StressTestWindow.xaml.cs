using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;
using LaundryMachine.LaundryCode;
using LaundryMachineUi;

namespace LaundryStressTest
{
    using StressSimulationFactory = Func<TimeSpan, TimeSpan, TimeSpan, uint, ILaundryStressSimulation>;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            _stressSimFactory = new LocklessLazyWriteOnce<StressSimulationFactory>(InitStressSimulationFactory);
            btnInitialize.IsEnabled = true;
        }

        private void InitializeSimulation()
        {
            VerifyAccess();
            DestroySimulation();
            StressSimulationFactory factory = _stressSimFactory;
            TimeSpan addDamp, removeDirt, removeDamp;
            uint dirtyArticles;
            try
            {
                addDamp = TimeSpan.FromMilliseconds(double.Parse(txbxWetTimeMilliseconds.Text));
                removeDirt = TimeSpan.FromMilliseconds(double.Parse(txbxCleansePerUnit.Text));
                removeDamp = TimeSpan.FromMilliseconds(double.Parse(txbxDryPerUnit.Text));
                dirtyArticles = uint.Parse(txbxStartDirty.Text);
                ILaundryStressSimulation simulation = factory(addDamp, removeDirt, removeDamp, dirtyArticles);
                _simulation = simulation;
                Debug.Assert(_simulation != null);
                txbxDirtyCount.Text = dirtyArticles.ToString();
                btnStart.IsEnabled = true;
                btnInitialize.IsEnabled = false;
                btnAbortSimulation.IsEnabled = true;
                ListenToSimEvents(true);
                lmCtrlPnl1.RefreshStateCode();
                lmCtrlPnl2.RefreshStateCode();
                lmCtrlPnl3.RefreshStateCode();
                
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                DestroySimulation();
                MessageBox.Show(ex.ToString(), "Bad Parse or Out of Range", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void _simulation_SimulationEnded(object sender, SimulationEndedEventArgs e)
        {
            PostAction(async delegate
            {
               
                Debug.WriteLine(e.ToString());
                Console.WriteLine(@$"Simulation ended event args: {e}.");
                string header = Title;
                Title = "Processing results, please wait ...";
                var sim = _simulation;
                btnStart.IsEnabled = false;
                btnAbortSimulation.IsEnabled = false;
                btnStart.IsEnabled = false;

                (bool Success, string Explanation) res = await sim.EvaluateResultsAsync();
                DisplayResults(res, header);

            });

            void DisplayResults((bool Success, string Explanation) res, string headerToRestore) => PostAction(delegate
            {
                if (res.Success)
                {
                    MessageBox.Show(res.Explanation, "Simulation Succeeded", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    Console.Error.WriteLineAsync(res.Explanation);
                    Debug.WriteLine(res.Explanation);
                    MessageBox.Show(res.Explanation, "Simulation FAILED", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                DestroySimulation();
                btnInitialize.IsEnabled = true;
                Title = headerToRestore;
            });
            

        }


        private void _simulation_SimulationFaulted(object sender, ExceptionEventArgs e) => PostAction(() =>

            MessageBox.Show(this, $"The simulation faulted.  Error details: [{e}].", "FAULTED!",
                MessageBoxButton.OK, MessageBoxImage.Error));
        

        private void ListenToSimEvents(bool listen)
        {
            VerifyAccess();
            ILaundryStressSimulation simulation = _simulation;
            if (simulation == null) return;
            Debug.Assert(_simulation.LaundryMachines.Count == 3);
            Debug.Assert(_simulation.LoaderRobots.Count == 2);
            Debug.Assert(_simulation.UnloaderRobots.Count == 2);
            if (_listening)
            {
                simulation.SimulationFaulted -= _simulation_SimulationFaulted;
                simulation.SimulationEnded -= _simulation_SimulationEnded;
                lmCtrlPnl1.TryReleaseMachine();
                lmCtrlPnl2.TryReleaseMachine();
                lmCtrlPnl3.TryReleaseMachine();

                simulation.LoaderRobots[0].RobotActed -= LoaderRobotOne_Acted;
                simulation.LoaderRobots[1].RobotActed -= LoaderRobotTwo_Acted;


                simulation.UnloaderRobots[0].RobotActed -= UnloaderRobotOne_Acted;
                simulation.UnloaderRobots[1].RobotActed -= UnloaderRobotTwo_Acted;

                simulation.CleanBin.ContentsChanged -= CleanBin_ContentsChanged;
                simulation.DirtyBin.ContentsChanged -= DirtyBin_ContentsChanged;
                _listening = false;
            }

            if (listen)
            {
                if (_listening) throw new InvalidOperationException("Already listening!");
                _listening = true;
                simulation.SimulationFaulted += _simulation_SimulationFaulted;
                simulation.SimulationEnded += _simulation_SimulationEnded;
                


                lmCtrlPnl1.SupplyLaundryMachine(simulation.LaundryMachines[0], simulation.LaundryEventPublisher[0]);
                lmCtrlPnl2.SupplyLaundryMachine(simulation.LaundryMachines[1], simulation.LaundryEventPublisher[1]);
                lmCtrlPnl3.SupplyLaundryMachine(simulation.LaundryMachines[2], simulation.LaundryEventPublisher[2]);
                
                lmCtrlPnl1.RefreshStateCode();
                lmCtrlPnl2.RefreshStateCode();
                lmCtrlPnl3.RefreshStateCode();

                simulation.LoaderRobots[0].RobotActed += LoaderRobotOne_Acted;
                simulation.LoaderRobots[1].RobotActed += LoaderRobotTwo_Acted;

                
                simulation.UnloaderRobots[0].RobotActed += UnloaderRobotOne_Acted;
                simulation.UnloaderRobots[1].RobotActed += UnloaderRobotTwo_Acted;

                simulation.CleanBin.ContentsChanged += CleanBin_ContentsChanged;
                simulation.DirtyBin.ContentsChanged += DirtyBin_ContentsChanged;

                txbxCleanCount.Text = simulation.CleanBin.Count.ToString();
                txbxDirtyCount.Text = simulation.DirtyBin.Count.ToString();
            }

        }

        private void DirtyBin_ContentsChanged(object sender, LaundryRepositoryEventArgs e)
        {
            if (e != null)
            {
                PostAction(() => 
                {
                    int count = int.Parse(txbxDirtyCount.Text);
                    string newCount = e.AddedToRepo ? (++count).ToString() : (--count).ToString();
                    txbxDirtyCount.Text = newCount;
                    LogEventToTextBox(rtxbxDirtyLog, e);
                });
            }
        }
        

        private void CleanBin_ContentsChanged(object sender, LaundryRepositoryEventArgs e)
        {
            if (e != null)
            {
                PostAction(() =>
                {
                    int count = int.Parse(txbxCleanCount.Text);
                    string newCount = e.AddedToRepo ? (++count).ToString() : (--count).ToString();
                    txbxCleanCount.Text = newCount;
                    LogEventToTextBox(rtxbxCleanyLog, e);
                });
            }
        }

        private void UnloaderRobotTwo_Acted(object sender, RobotActedEventArgs e)
        {
            if (e != null)
            {
                PostAction(() =>
                {
                    LogEventToTextBox(rtbxUnloaderTwoLog, e);
                });
            }
        }

        private void UnloaderRobotOne_Acted(object sender, RobotActedEventArgs e)
        {
            if (e != null)
            {
                PostAction(() =>
                {
                    LogEventToTextBox(rtbxUnloaderOneLog, e);
                });
            }
        }

        private void LoaderRobotTwo_Acted(object sender, RobotActedEventArgs e)
        {
            if (e != null)
            {
                PostAction(() =>
                {
                    LogEventToTextBox(rtbxLoaderTwoLog, e);
                });
            }
        }

        private void LoaderRobotOne_Acted(object sender, RobotActedEventArgs e)
        {
            if (e != null)
            {
                PostAction(() =>
                {
                    LogEventToTextBox(rtbxLoaderOneLog, e);
                });
            }
        }

        private void PostAction([NotNull] Action a)
        {
            var dispatcher = Dispatcher;
            
            if (CheckAccess())
            {
                a();
            }
            else
            {
                dispatcher?.InvokeAsync(a)?.FireAndForget();
            }
        }

        private void btnInitialize_Click(object sender, RoutedEventArgs e)
            => InitializeSimulation();

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnInitialize.IsEnabled = false;
                btnStart.IsEnabled = false;
                btnAbortSimulation.IsEnabled = true;
                _simulation.StartSimulation();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                DestroySimulation();
                btnInitialize.IsEnabled = true;
                btnStart.IsEnabled = false;
                btnAbortSimulation.IsEnabled = false;
                MessageBox.Show($"Unable to start simulation.  Error: [{ex}].");
            }
        }

        private void btnAbortSimulation_Click(object sender, RoutedEventArgs e) => DestroySimulation();
        
        private void LogEventToTextBox<TEventArgs>([NotNull] RichTextBox rtbx, [NotNull] TEventArgs args, [CanBeNull] Func<TEventArgs, string> stringifier = null)
            where TEventArgs : EventArgs =>
            LogMessageToTextBox(rtbx, stringifier != null ? stringifier(args) : args.ToString());

        private void LogMessageToTextBox([NotNull] RichTextBox rtbx, string message)
        {
            VerifyAccess();
            rtbx.AppendText(message + Environment.NewLine);
            rtbx.ScrollToEnd();
        }

        private void DestroySimulation()
        {
            VerifyAccess();
            ListenToSimEvents(false);
            ILaundryStressSimulation sim = _simulation;
            sim?.Dispose();
            _simulation = null;
            btnInitialize.IsEnabled = true;
            btnStart.IsEnabled = false;
            btnAbortSimulation.IsEnabled = false;
            _listening = false;
            ClearAllRichTextBoxes();
        }

        private void ClearAllRichTextBoxes()
        {
            rtbxLoaderOneLog.Document.Blocks.Clear();
            rtbxLoaderTwoLog.Document.Blocks.Clear();
            rtbxUnloaderOneLog.Document.Blocks.Clear();
            rtbxUnloaderTwoLog.Document.Blocks.Clear();
            rtxbxCleanyLog.Document.Blocks.Clear();
            rtxbxDirtyLog.Document.Blocks.Clear();
            txbxCleanCount.Text = "0";
            txbxDirtyCount.Text = "0";
        }


        protected sealed override void OnClosed(EventArgs e)
        {
            try
            {
                ListenToSimEvents(false);
                _simulation?.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
            }
        }
        private void btnInitialize_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            txbxCleansePerUnit.IsReadOnly = !btnInitialize.IsEnabled;
            txbxDryPerUnit.IsReadOnly = !btnInitialize.IsEnabled;
            txbxWetTimeMilliseconds.IsReadOnly = !btnInitialize.IsEnabled;
        }
        private bool _listening;
        protected virtual StressSimulationFactory InitStressSimulationFactory() =>
            LaundryStressSimulation.CreateSimulation;
        private ILaundryStressSimulation _simulation;
        private readonly LocklessLazyWriteOnce<StressSimulationFactory> _stressSimFactory;

        
    }
}
