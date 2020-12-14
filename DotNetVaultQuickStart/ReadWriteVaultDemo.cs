using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using DotNetVault.Exceptions;
using DotNetVault.Vaults;
using HpTimeStamps;
using JetBrains.Annotations;

namespace DotNetVaultQuickStart
{
    /// <summary>
    /// This class demonstrates how to use the ReadWriteVault with a large(ish)
    /// mutable struct as its status flag (i.e. its protected resource).
    ///
    /// There are four active threads that interact with the vault.
    ///
    /// Two of the threads are "reader threads" -- they obtain readonly locks (potentially concurrently)
    /// examine the status flag and log its current state.  Each will terminate when cancelled or when the state is detected
    /// to be "Done".
    ///
    /// One thread is a regular writer thread.  The writer thread obtains a write lock and attempts to increment
    /// the item count by a random amount by calling the "Increment" mutator method on the flags.  This thread terminates
    /// when the "Increment" method returns false -- (which only happens when it has already reached the "Done" state)
    ///
    /// The final thread demonstrates the upgradable readonly lock.
    /// It examines the flag's <see cref="SharedFlags.CurrentAction"/> property and its <see cref="SharedFlags.ItemCount"/> property.
    /// If the item count meets or exceeds the (arbitrarily set) threshold to do to the next state, then
    ///     1- it upgrades the upgradable read-only lock to a write lock (note that the special feature of the upgradable readonly lock
    ///        is that it can be upgraded to a write lock WITHOUT RELEASING its readonly lock ... ensuring that the current state
    ///        of the resource remains consistent (if it had to release the readonly lock before getting a write lock, another thread
    ///        could change the state of the resource before the write lock could be obtained).
    ///     2- it then calls the appropriate mutator method on the flags to advance it to the next <see cref="ActiveAction"/> state; it also
    ///        saves a string representation of the next state that it logs AFTER it releases its locks (to avoid IO while holding lock)
    ///     3- the write lock is then released (by going out of scope), leaving it with an upgradable readonly lock, which is in turn released
    ///        when its scope ends.
    /// This thread ends when the <see cref="ActiveAction.Done"/> is reached.     
    ///       
    /// </summary>
    sealed class ReadWriteVaultDemo
    {
        /// <summary>
        /// Run the demo.
        ///
        /// After the demo is complete, dump the log to a string and return it.
        /// </summary>
        /// <param name="wait">the maximum amount of time to wait for the demo to complete.  On some systems,
        /// (without many cores) the value chosen for this demo may need to be raised.  For my six core, twelve-thread
        /// (hyper threading) machine 7.5 seconds is more than enough.</param>
        /// <returns>The log dumped to a string, or empty string.</returns>
        [NotNull]
        public static string RunDemo(TimeSpan wait)
        {
            if (wait <= TimeSpan.Zero) throw new ArgumentNotPositiveException<TimeSpan>(nameof(wait), wait);
            ReadWriteVaultDemo d = new ReadWriteVaultDemo();
            
            //start the demos master thread (which will start other threads and wait for them to complete or timeout)
            d._masterThread.Start(wait);
            //join the master thread -- will end when demo completes, timeouts or otherwise faults
            d._masterThread.Join();
            try
            {
                //dispose logger
                d._log.Dispose();
            }
            catch
            {
                //eat
            }

            try
            {
                //signal cancel on token
                d._cts.Cancel();
            }
            catch
            {
                //eat
            }

            try
            {
                //dispose token
                d._cts.Dispose();
            }
            catch 
            {
                //eat
            }
            //return the logged info from the disposed log if available
            return d._log.Text ?? string.Empty;
        }

        private ReadWriteVaultDemo()
        {
            //NOTE: CTOR sets up but does not actually START any threads

            //set up reader threads
            _readerThreads = new Thread[2];
            for (int i = 0; i < 2; ++i)
            {
                _readerThreads[i] = new Thread(ReaderThread);
            }
            
            //setup upgradable readonly and writer thread
            _upgradeRoThread = new Thread(UpgradableThread);
            _writerThread = new Thread(WriterThread);

            //setup the master thread (which, when started, starts the other threads then joins them, timing out if
            //it cannot join in time.  If timed out or otherwise, it will use a cancellation token to terminate all still-running threads
            _masterThread = new Thread(MasterThread);
        }

        private void MasterThread(object ts)
        {
            TimeSpan wait = (TimeSpan) ts;
            _upgradeRoThread.Start(_cts.Token);
            int rdrThreadNo = 0;
            foreach (var thread in _readerThreads)
            {
                thread.Start(new ValueTuple<CancellationToken, int>(_cts.Token, ++rdrThreadNo));
            }
            _writerThread.Start(_cts.Token);

            try
            {
               bool done = _upgradeRoThread.Join(wait);
               if (!done)
               {
                   throw new TimeoutException("The game did not complete within [" + wait.TotalSeconds.ToString("F3") + "] seconds.");
               }
            }
            catch (Exception ex)
            {
                _log.Log($"Demo faulted: {ex}");
            }

            try
            {
                _cts.Cancel();
            }
            catch (Exception)
            {
                // eat
            }

            _upgradeRoThread.Join();
            _writerThread.Join();
            foreach (var reader in _readerThreads)
            {
                reader.Join();
            }
            _log.Dispose();

        }

        /// <summary>
        /// Reader thread routine (multiple reader threads running simultaneously)
        /// </summary> 
        /// <param name="threadNoObj">Should be a value tuple whose first field is a <see cref="CancellationToken"/> and whose
        /// second field is an <see cref="System.Int32"/> (which serves as its thread number for logging purposes.</param>
        private void ReaderThread(object threadNoObj)
        {
            if (threadNoObj is ValueTuple<CancellationToken, int> tuple)
            {
                CancellationToken token = tuple.Item1;
                int threadNum = tuple.Item2;
                while (true)
                {
                    ActiveAction currentAction = ActiveAction.None;
                    string currentStatus = null;
                    try
                    {
                        //get readonly lock .... readonly lock is released at end of try block
                        using var roLck = _sharedFlagsVault.RoLock(TimeSpan.FromMilliseconds(250), token);
                        //can only access properties whose getters are explicitly or implicitly readonly (enforced on pain of compiler error)
                        //can only access methods that are marked readonly.  
                        currentStatus = roLck.Value.ToString();
                        currentAction = roLck.Value.CurrentAction;

                        //note that you can (without error but with warning) access a mutator METHOD ... BUT, the entire resource will 
                        //be deep copied and the mutation will be called on the deep copy rather than the actual protected resource stored
                        //in the vault ... example- if uncommented, will generate warning (and probably throw InvalidOperationException, unless
                        //current ActiveAction happens to be None):
                        //roLck.Value.Frobnicate();
                        
                    }//N.B. roLck is released here: its scope ended.
                    catch (TimeoutException ex)
                    {
                        //log a timeout but not considered a fault
                        _log.Log($"Reader thread {threadNum} timed out: [" + ex + "].");
                    }
                    catch (OperationCanceledException)
                    {
                        //RoLock operation was cancelled via the token
                        return;
                    }

                    if (currentStatus != null)
                    {
                        //log the info it obtained above ... best to do this as here ...
                        //after lock is released
                        _log.Log("From reader #" + threadNum + ": " + currentStatus);
                    }

                    if (currentAction == ActiveAction.Done)
                    {
                        return; //if action is done, thread ends
                    }

                    //if os thinks good idea, let other threads have a turn
                    Thread.Yield();
                }
            }
        }

        /// <summary>
        /// Write thread routine goes here
        /// </summary>
        /// <param name="ctObject">should be a cancellation token.</param>
        private void WriterThread(object ctObject)
        {
            //random generator for the thread
            Random r = new Random();
            if (ctObject is CancellationToken token)
            {
                Span<byte> bytes = stackalloc byte[8]; //getting a random ulong (could probably just get an int, but this is cool)
                while (true) //termination condition is ActiveAction == Done (signaled by Increment method returning false) OR
                {            //cancellation request propagated through token
                    r.NextBytes(bytes); //get random
                    ulong increment = BitConverter.ToUInt64(bytes) % 103; //mod by 103 ... range ergo 0 ... 102
                    try
                    {
                        {
                            //get a write lock
                            using var lck = _sharedFlagsVault.Lock(TimeSpan.FromMilliseconds(750), token);
                            if (!lck.Value.Increment(increment)) //call mutator method that increments ItemCountProperty
                            {
                                return; //if it returns false, it means ActiveAction is Done
                                //so thread should end
                            }
                        }//lock released here so we do not yield while holding lock
                        token.ThrowIfCancellationRequested();
                        Thread.Yield(); //if os thinks good idea, let other threads have a turn
                    }
                    catch (OperationCanceledException)
                    {
                        return; //token propagated cancel request... end thread.
                    }
                    catch (TimeoutException ex)
                    {
                        //timeout is logged but not considered a fault
                        _log.Log("Writer thread timed out: [" + ex + "].");
                    }
                }
            }
        }
        /// <summary>
        /// Upgradable thread routine
        /// </summary>
        /// <param name="ctObject">should be a cancellation token.</param>
        private void UpgradableThread(object ctObject)
        {
            try
            {
                if (ctObject is CancellationToken token)
                {
                    //if we call mutator on a loop iteration, 
                    //store changed to state
                    ActiveAction? changedToAction = null;
                    //last known current action
                    ActiveAction lastKnownAction = ActiveAction.None;
                    while (lastKnownAction != ActiveAction.Done) // finish when we get to done
                    {
                        try
                        {
                            changedToAction = null; //clearout log text from last iteration
                            
                            //Obtain an upgradable readonly lock here
                            using var upgrLck 
                                = _sharedFlagsVault.UpgradableRoLock(TimeSpan.FromMilliseconds(500), token);
                            //get current action from flags
                            lastKnownAction = upgrLck.Value.CurrentAction;
                            
                            //Switch on the action 
                            switch (lastKnownAction)
                            {
                                //if none, always frobnicate
                                case ActiveAction.None:
                                    {
                                        //upgrade to write lock (WITHOUT RELEASING readonly lock!)
                                        using var lck = upgrLck.Lock(TimeSpan.FromMilliseconds(750), token);
                                        lck.Value.Frobnicate(); //call mutator
                                        changedToAction = ActiveAction.Frobnicating; //store the fact we are frobnicating 
                                        //we could have to stringed it here, but that may cause dynamic allocation ... bad to do holding lock
                                    } //the write lock is released here ... WE STILL HOLD UPGRADABLE RO LOCK
                                    break;
                                //if frobnicating, change to prognosticating if and only if ItemCount at least "PrognosticateAt" value
                                case ActiveAction.Frobnicating:
                                    if (upgrLck.Value.ItemCount >= PrognosticateAt)
                                    { //upgrade to write lock (WITHOUT RELEASING readonly lock!)
                                        using var lck = upgrLck.Lock(TimeSpan.FromMilliseconds(750), token);
                                        lck.Value.Prognosticate(); //call mutator
                                        changedToAction = ActiveAction.Prognosticating;
                                        //we could have to stringed it here, but that may cause dynamic allocation ... bad to do holding lock
                                    }//the write lock is released here ... WE STILL HOLD UPGRADABLE RO LOCK
                                    break;
                                //if Prognosticating, change to Procrastinate if and only if ItemCount at least "ProcrastinateAt" value
                                case ActiveAction.Prognosticating:
                                    if (upgrLck.Value.ItemCount >= ProcrastinateAt)
                                    {  //upgrade to write lock (WITHOUT RELEASING readonly lock!)
                                        using var lck = upgrLck.Lock(TimeSpan.FromMilliseconds(750), token);
                                        lck.Value.Procrastinate();//call mutator
                                        changedToAction = ActiveAction.Procrastinating;
                                        //we could have to stringed it here, but that may cause dynamic allocation ... bad to do holding lock
                                    }//the write lock is released here ... WE STILL HOLD UPGRADABLE RO LOCK
                                    break;
                                //if Procrastinating, change to Dithering if and only if ItemCount at least "DitherAt" value
                                case ActiveAction.Procrastinating:
                                    if (upgrLck.Value.ItemCount >= DitherAt)
                                    {   //upgrade to write lock (WITHOUT RELEASING readonly lock!)
                                        using var lck = upgrLck.Lock(TimeSpan.FromMilliseconds(750), token);
                                        lck.Value.Dither(); //call mutator
                                        changedToAction = ActiveAction.Dithering;
                                        //we could have to stringed it here, but that may cause dynamic allocation ... bad to do holding lock
                                    } //the write lock is released here ... WE STILL HOLD UPGRADABLE RO LOCK
                                    break;
                                //if dithering, change to Done if and only if ItemCount at least "DoneAt" value
                                case ActiveAction.Dithering:
                                    if (upgrLck.Value.ItemCount >= DoneAt)
                                    {
                                        //upgrade to write lock (WITHOUT RELEASING readonly lock!)
                                        using var lck = upgrLck.Lock(TimeSpan.FromMilliseconds(750), token);
                                        lck.Value.Finish();//call mutator
                                        changedToAction = ActiveAction.Done;
                                    }
                                    break;
                                case ActiveAction.Done:
                                    //don't do anything, we gonna quit
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException($"Enum value: {lastKnownAction} is not understood.");
                            }
                        } //UPGRADABLE RO LOCK RELEASED HERE
                        catch (TimeoutException ex)
                        {
                            _log.Log("Timeout in upgradable thread: [" + ex + "].");
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        if (changedToAction != null)
                        {
                            //to string here ... no locks currently done
                            Console.WriteLine(changedToAction);
                        }
                        else if (lastKnownAction != ActiveAction.Done)
                        {
                            //if we didn't just do io and we aren't done, 
                            //then let os give another thread a turn if it thinks good idea.
                            Thread.Yield();
                        }
                    }
                }
            }
            finally
            {
                _done.TrySet();
            }
        }
        private const ulong PrognosticateAt = 145_000;
        private const ulong ProcrastinateAt = 250_000;
        private const ulong DitherAt = 375_000;
        private const ulong DoneAt = 500_000;
        //private const ulong PrognosticateAt = 100;
        //private const ulong ProcrastinateAt = 6969;
        //private const ulong DitherAt = 20_921;
        //private const ulong DoneAt = 100_092;
        [NotNull] private readonly Thread _masterThread;
        [NotNull] private readonly Thread _writerThread;
        [NotNull] [ItemNotNull] private readonly Thread[] _readerThreads;
        [NotNull] private readonly Thread _upgradeRoThread;
        private LocklessSetOnceBool _done;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly BasicReadWriteVault<SharedFlags> _sharedFlagsVault =
            new BasicReadWriteVault<SharedFlags>(SharedFlags.CreateSharedFlags(), TimeSpan.FromSeconds(2));
        [NotNull] private readonly Logger _log = Logger.CreateLogger();
    }

    sealed class Logger : IDisposable
    {
        public static Logger CreateLogger()
        {
            Logger l = new Logger();
            try
            {
                DateTime quitAfter = TimeStampSource.Now + TimeSpan.FromMilliseconds(500);
                l._loggingThread.Start(l._cts.Token);
                while (!l._start.IsSet && TimeStampSource.Now < quitAfter)
                {
                    System.Threading.Thread.Yield();
                }

                if (!l._start.IsSet)
                {
                    throw new TimeoutException("Could not verify start of logger thread.");
                }

                return l;
            }
            catch (Exception ex)
            {
                l.Dispose();
                Console.Error.WriteLine(ex);
                throw;
            }

            
        }

        public string Text
        {
            get
            {
                string txt = _resultText;
                return txt ?? string.Empty;
            }
        }
        public void Log(string x)
        {
            if (_disposed.IsSet) return;
            try
            {
                _logCollection.Add(x);
            }
            catch (ObjectDisposedException)
            {
                //eat
            }
        }

        public void Dispose() => Dispose(true);

        private Logger()
        {
            _logCollection = new BlockingCollection<Log>(new ConcurrentQueue<Log>());
            _loggingThread = new Thread(Thread) {Name = "ReadWriteVaultLog", IsBackground = true, Priority = ThreadPriority.Lowest};
        }

        private void Dispose(bool disposing)
        {
            if (_disposed.TrySet() && disposing)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (Exception)
                {
                    //eat
                }

                try
                {
                    _cts.Dispose();
                }
                catch
                {
                    //eat
                }

                try
                {
                    _logCollection.Dispose();
                }
                catch (Exception)
                {
                    // eat
                }

                _loggingThread.Join();
            }
        }

        private void Thread(object cancTkn)
        {
            _start.SetOrThrow();
            try
            {
                if (cancTkn is CancellationToken token)
                {
                    while (true)
                    {
                        try
                        {
                            Log l = _logCollection.Take(token);
                            _sb.AppendLine(l.ToString());
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex.ToString());
                            throw;
                        }
                    }
                }
            }
            finally
            {
                string text = _sb.ToString();
                Interlocked.Exchange(ref _resultText, text);
                _sb.Clear();
                _sb.Capacity = 4;
            }

        }

        [CanBeNull] private volatile string _resultText;
        [NotNull] private readonly StringBuilder _sb = new StringBuilder();
        [NotNull] private readonly BlockingCollection<Log> _logCollection;
        private LocklessSetOnceBool _disposed;
        private LocklessSetOnceBool _start;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly Thread _loggingThread;
    }

    readonly struct Log : IEquatable<Log>, IComparable<Log>
    {
        public static implicit operator Log(string l) => new Log(TimeStampSource.Now, l);
        

        public DateTime TimeStamp { get; }
        [NotNull] public string Text => _text ?? string.Empty;

        private Log(DateTime stamp, string text)
        {
            _text = text ?? string.Empty;
            TimeStamp = stamp;
        }

        public static bool operator ==(in Log lhs, in Log rhs) => lhs.TimeStamp == rhs.TimeStamp &&
                                                                  string.Equals(lhs.Text, rhs.Text,
                                                                      StringComparison.Ordinal);
        public static bool operator !=(in Log lhs, in Log rhs) => !(lhs == rhs);
        public override int GetHashCode() => TimeStamp.GetHashCode();
        public override bool Equals(object obj) => obj is Log l && l == this;
        public static bool operator >(in Log lhs, in Log rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in Log lhs, in Log rhs) => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in Log lhs, in Log rhs) => !(lhs < rhs);
        public static bool operator <=(in Log lhs, in Log rhs) => !(lhs > rhs);
        public int CompareTo(Log other) => Compare(in this, other);
        public bool Equals(Log other) => other == this;
        public override string ToString() => "At [" + TimeStamp.ToString("O") + "]:\t\t " + Text;
        

        public static int Compare(in Log lhs, in Log rhs)
        {
            int tsCompare = lhs.TimeStamp.CompareTo(rhs.TimeStamp);
            return tsCompare == 0 ? string.Compare(lhs.Text, rhs.Text, StringComparison.Ordinal) : tsCompare;
        }

        private readonly string _text;
    }

    struct LocklessSetOnceBool
    {
        public bool IsSet
        {
            get
            {
                int state = _state;
                return state == Set;
            }
        }

        public bool TrySet() => Interlocked.CompareExchange(ref _state, Set, NotSet) == NotSet;

        public void SetOrThrow()
        {
            if (!TrySet()) throw new InvalidOperationException("Already set.");
        }

        private volatile int _state;
        private const int NotSet = 0;
        private const int Set = 1;
    }
}
