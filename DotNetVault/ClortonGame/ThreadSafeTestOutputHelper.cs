using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using DotNetVault.Logging;
using DotNetVault.TestCaseHelpers;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using SetOnceValFlag = DotNetVault.ToggleFlags.SetOnceValFlag;
using TimeStampSource = DotNetVault.ClortonGame.CgTimeStampSource;
namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// A helper for i/o
    /// </summary>
    public class ThreadSafeTestOutputHelper : IEventRaisingOutputHelper
    {
        #region Factory method
        /// <summary>
        /// Factory method
        /// </summary>
        /// <param name="helper">helper to wrap</param>
        /// <returns>the output helper</returns>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> was <see langword="null"/></exception>
        public static IEventRaisingOutputHelper CreateOutputHelper([NotNull] IOutputHelper helper)
            => new ThreadSafeTestOutputHelper(helper); 
        #endregion
        
        #region Public Properties and Events
        /// <inheritdoc />
        public event EventHandler<OutputHelperAppendedToEventArgs> TextAppended;
        /// <inheritdoc />
        public bool IsDisposed => _disposed.IsSet; 
        #endregion

        #region CTOR
        private ThreadSafeTestOutputHelper([NotNull] IOutputHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _outputHelperExecutor =
                Executor.CreateExecutor("OutputHelperEvents", str => new TimeStampCalibratingExecutor(str));
            _thread = new Thread(ThreadLoop) { Name = "DisposableOutputHelper", IsBackground = true };
            _thread.Start(_cts.Token);
            DateTime quitAt = TimeStampSource.Now + TimeSpan.FromMilliseconds(500);
            while (!_started.IsSet && TimeStampSource.Now <= quitAt)
            {
                Thread.Sleep(1);
            }

            if (!_started.IsSet)
            {
                throw new InvalidOperationException("Couldn't start thread.");
            }
        }
        #endregion

        #region Public Methods
        /// <inheritdoc />
        public void WriteLine(string message)
        {
            if (_started.IsSet && !_terminated.IsSet && !_disposed.IsSet)
            {
                PerformWriteLine(message, null);
            }
        }

        /// <inheritdoc />
        public void WriteLine(string format, params object[] args)
        {
            if (_started.IsSet && !_terminated.IsSet && !_disposed.IsSet)
            {
                PerformWriteLine(format, args);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        } 
        #endregion

        #region Protected Methods
        /// <summary>
        /// add an action to queue that will append the text
        /// </summary>
        /// <param name="format">string format</param>
        /// <param name="args">format args, if any.</param>
        protected virtual void PerformWriteLine(string format, params object[] args)
        {
            try
            {
                _collection.Add(Execute);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                throw;
            }

            void Execute()
            {
                string toBeWritten = GetString();
                try
                {
                    OutputHelperAppendedToEventArgs e = new OutputHelperAppendedToEventArgs(toBeWritten);
                    _helper.WriteLine(toBeWritten);
                    OnTextAppended(e);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                    throw;
                }
            }

            string GetString()
            {
                if (format == null) return string.Empty;
                if (args == null) return format;
                try
                {
                    return string.Format(format, args);
                }
                catch (FormatException ex)
                {
                    return $"The format string [{format}] threw a format exception: [{ex}].";
                }
            }
        }

        /// <summary>
        /// Dispose the helper
        /// </summary>
        /// <param name="disposing">true means called by code, false means called by garbage collector
        /// during a finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed.TrySet() && disposing)
            {
                if (!_terminated.IsSet && _started.IsSet)
                {
                    _cts.Cancel();
                    _thread.Join();
                }
                _outputHelperExecutor.Dispose();
                _collection.Dispose();
                _cts.Dispose();
            }
        }

        /// <summary>
        /// Event invocator for <see cref="TextAppended"/> <see langword="event"/>
        /// </summary>
        /// <param name="e">event arguments</param>
        protected virtual void OnTextAppended(OutputHelperAppendedToEventArgs e)
        {
            if (e != null)
            {
                try
                {
                    _outputHelperExecutor.EnqueueAction(() => TextAppended?.Invoke(this, e));
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }
        #endregion

        #region Private Methods
        private void ThreadLoop(object tokenObj)
        {
            if (_started.TrySet() && tokenObj is CancellationToken token)
            {
                try
                {
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        Action doMe = _collection.Take(token);
                        if (doMe != null)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                doMe();
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLineAsync(ex.ToString());
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    Console.Error.WriteAsync(ex.ToString());
                    throw;
                }
                finally
                {
                    _terminated.TrySet();
                }
            }
            else
            {
                throw new InvalidOperationException("Thread already started.");
            }
        }
        #endregion

        #region Private Data
        private SetOnceValFlag _terminated = default;
        private SetOnceValFlag _started = default;
        private SetOnceValFlag _disposed = default;
        [NotNull] private readonly IOutputHelper _helper;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly BlockingCollection<Action> _collection = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
        [NotNull] private readonly Thread _thread;
        [NotNull] private readonly IExecutor _outputHelperExecutor; 
        #endregion
    }

    /// <summary>
    /// An output helper that maintains log entries in order based on time stamp (first) and message
    /// content (second)
    /// </summary>
    public class OrderedThreadSafeTestOutputHelper : 
        IBufferBasedOutputHelper, IEventRaisingOutputHelper
    {
        #region Factory Method
        /// <summary>
        ///  Create an instance.
        /// </summary>
        /// <returns>an instance</returns>
        /// <exception cref="Exception">Error creating helper or verifying
        /// that all component started.</exception>
        public static OrderedThreadSafeTestOutputHelper CreateInstance()
        {
            var ret = new OrderedThreadSafeTestOutputHelper();
            try
            {
                ret.StartThread();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                ret.Dispose();
                throw;
            }
            return ret;
        } 
        #endregion

        #region Properties and Events

        /// <summary>
        /// If an exception caused thread not to start, it's here.
        /// </summary>
        [CanBeNull]
        public Exception ThreadStartException
        {
            get
            {
                Exception ret = _threadStartException;
                return ret;
            }
        }

        /// <inheritdoc />
        public event EventHandler<OutputHelperAppendedToEventArgs> TextAppended;
        /// <summary>
        /// True if thread start ever requested, false otherwise
        /// </summary>
        public bool ThreadStartEverRequested => _threadStartRequested.IsSet;
        /// <summary>
        /// true if thread currently active, false otherwise
        /// </summary>
        public bool ThreadActive => _threadStarted.IsSet && _threadTerminated.IsClear;
        /// <summary>
        /// true if disposed, false otherwise
        /// </summary>
        public bool IsDisposed => _disposed.IsSet;
        /// <summary>
        /// The concrete type of this instance
        /// </summary>
        [NotNull] protected Type ConcreteType => _concreteType.Value; 
        #endregion

        #region CTOR
        /// <summary>
        /// CTOR
        /// </summary>
        protected OrderedThreadSafeTestOutputHelper()
        {
            _concreteType = new LocklessConcreteType(this);
            long threadNum = Interlocked.Increment(ref _instanceCount);
            _thread = new Thread(ThreadLoop) { IsBackground = true, Name = string.Format(ThreadNamePrefix, threadNum) };
            _eventThread = Executor.CreateExecutor($"OutputEventThd_{threadNum}");
        } 
        #endregion

        #region Public Methods
        /// <inheritdoc />
        public void WriteLine(string message)
        {
            ThrowIfDisposed();
            DateTime ts = CgTimeStampSource.Now;
            var content = new OutputHelperContent(ts, message);
            OnTextAppended(in content);
            _blockingCollection.Add(content);
        }

        /// <inheritdoc />
        public void WriteLine(string format, params object[] args)
        {
            ThrowIfDisposed();
            DateTime ts = CgTimeStampSource.Now;
            string writeMe = string.Format(format, args);
            var content = new OutputHelperContent(ts, writeMe);
            OnTextAppended(in content);
            _blockingCollection.Add(content);
        }

        /// <inheritdoc />
        public string GetCurrentText(TimeSpan timeout)
        {
            ImmutableSortedSet<OutputHelperContent> imss;
            {
                using var lck = _resourceVault.SpinLock(timeout);
                imss = lck.ExecuteQuery((in SortedSet<OutputHelperContent> res) => res.ToImmutableSortedSet());
            }
            StringBuilder sb = new StringBuilder(imss.Count * 20);
            foreach (var item in imss)
            {
                sb.AppendLine(item.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public string GetCurrentTextAndClearBuffer(TimeSpan timeout)
        {
            ImmutableSortedSet<OutputHelperContent> imss;
            {
                using var lck = _resourceVault.SpinLock(timeout);
                imss = lck.ExecuteQuery((in SortedSet<OutputHelperContent> res) => res.ToImmutableSortedSet());
                lck.ExecuteAction((ref SortedSet<OutputHelperContent> res) => res.Clear());
            }
            StringBuilder sb = new StringBuilder(imss.Count * 20);
            foreach (var item in imss)
            {
                sb.AppendLine(item.ToString());
            }

            return sb.ToString();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Protected and private methods
        /// <summary>
        /// Start the thread.  Can call only once.
        /// </summary>
        /// <exception cref="InvalidOperationException">Called more than once,
        /// couldn't start thread or thread terminated prematurely.</exception>
        protected void StartThread()
        {
            _threadStartRequested.SetOrThrow();
            _thread.Start(_cts.Token);
            DateTime quitAfter = CgTimeStampSource.Now + TimeSpan.FromSeconds(1);
            while (!_threadStarted.IsSet && CgTimeStampSource.Now <= quitAfter)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(10));
            }



            if (!_threadStarted.IsSet)
            {
                Exception threadStartException = ThreadStartException;
                string threadStartExMsg = threadStartException != null
                    ? $"  Exception thrown by thread during start: [{threadStartException}]" : string.Empty;
                throw new InvalidOperationException(
                    $"Unable to start thread within one second.  Conditions: Request start: {_threadStartRequested.IsSet}; Started: {_threadStarted.IsSet}, Terminated: {_threadTerminated.IsSet}{threadStartExMsg}");
            }

            if (_threadTerminated.IsSet)
            {
                throw new InvalidOperationException("Thread terminated immediately after starting.");
            }
        }

        /// <summary>
        /// Throw if this object has been disposed
        /// </summary>
        /// <param name="caller">name of calling member</param>
        /// <exception cref="ObjectDisposedException">this object is disposed</exception>
        protected void ThrowIfDisposed([CallerMemberName] string caller = "")
        {
            if (IsDisposed)
                throw new ObjectDisposedException(ConcreteType.Name,
                    $"Illegal call to {caller}: object has been disposed.");
        }

        /// <summary>
        /// Stop the threads, free resources, etc
        /// </summary>
        /// <param name="disposing">true if called by code, false
        /// if called by GC during finalization</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed.TrySet() && disposing)
            {
                if (_thread.IsAlive)
                {
                    _cts.Cancel();
                }
                _thread.Join();

                _blockingCollection.Dispose();
                _cts.Dispose();
                _eventThread.Dispose();
            }
            TextAppended = null;
        }

        private void ThreadLoop(object cancelTokenObj)
        {
            bool threadStartedRightNow = _threadStarted.IsSet;
            try
            {
              
                if (_threadStarted.TrySet() && cancelTokenObj is CancellationToken token)
                {
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        var dequeued = _blockingCollection.Take(token);
                        if (dequeued != default)
                        {

                            using var lck = _resourceVault.Lock();
                            lck.ExecuteAction(
                                (ref SortedSet<OutputHelperContent> res, in OutputHelperContent content) =>
                                    res.Add(content), in dequeued);
                        }
                    }
                }
                else
                {
                    string text =
                        $"Unable to start thread.  Thread start status immediately at thread start: {_threadStarted.IsSet}; Thread started status now: {_threadStarted.IsSet}; Token object type: {cancelTokenObj?.GetType().ToString() ?? "NULL"}.";
                    var ex = new InvalidOperationException(text);
                    Interlocked.CompareExchange(ref _threadStartException, _threadStartException, null);
                    throw ex;
                }


            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
            finally
            {
                _threadTerminated.TrySet();
            }
        }

        /// <summary>
        /// Raises the <see cref="TextAppended"/> event on the <see cref="_eventThread"/> executor
        /// </summary>
        /// <param name="content">the stuff that was appended</param>
        protected virtual void OnTextAppended(in OutputHelperContent content)
        {
            OutputHelperContent copy = content;
            if (!_eventThread.Faulted && !_eventThread.IsDisposed && !_eventThread.Terminated && _eventThread.Started)
            {
                _eventThread.EnqueueAction(RaiseEvent);
            }

            void RaiseEvent()
            {
                var args = new OutputHelperAppendedToEventArgs(copy.TimeStamp, copy.Message);
                try
                {
                    TextAppended?.Invoke(this, args);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
        }
        #endregion

        #region Nested Type Def
        /// <summary>
        /// Contents stored within internal sorted set -- a time stamp and a message.
        /// </summary>
        protected readonly struct OutputHelperContent :
            IEquatable<OutputHelperContent>, IComparable<OutputHelperContent>
        {
            #region Static Comparison Method
            /// <summary>
            /// Compare to values
            /// </summary>
            /// <param name="lhs">the left-hand value</param>
            /// <param name="rhs">the right-hand value</param>
            /// <returns>0 if equal, a negative number if <paramref name="lhs"/> is less than
            /// <paramref name="rhs"/> and a positive number if <paramref name="lhs"/> is greater
            /// than <paramref name="rhs"/>.</returns>
            public static int Compare(in OutputHelperContent lhs, in OutputHelperContent rhs)
            {
                int stampComparison = lhs.TimeStamp.CompareTo(rhs.TimeStamp);
                return stampComparison != 0
                    ? stampComparison
                    : StringComparer.Ordinal.Compare(lhs.Message, rhs.Message);
            } 
            #endregion

            #region Public Fields
            /// <summary>
            /// The timestamp of the message
            /// </summary>
            public readonly DateTime TimeStamp;
            /// <summary>
            /// the message contents
            /// </summary>
            public readonly string Message;
            #endregion

            #region CTORS
            /// <summary>
            /// CTOR
            /// </summary>
            /// <param name="message">the message</param>
            public OutputHelperContent(string message) : this(CgTimeStampSource.Now, message) { }
            /// <summary>
            /// CTOR
            /// </summary>
            /// <param name="ts">timestamp</param>
            /// <param name="message">message</param>
            public OutputHelperContent(DateTime ts, string message)
            {
                TimeStamp = ts;
                Message = message ?? string.Empty;
            }
            #endregion

            #region Public Methods and Operators
            /// <inheritdoc />
            public override string ToString() =>
                "Logged at [" + TimeStamp.ToString("O") + "]:\t" + (Message ?? string.Empty);
            /// <summary>
            /// Test two values for equality
            /// </summary>
            /// <param name="lhs">left hand operand</param>
            /// <param name="rhs">right hand operand</param>
            /// <returns>true if values equal, false otherwise</returns>
            public static bool operator ==(in OutputHelperContent lhs, in OutputHelperContent rhs) =>
                lhs.TimeStamp == rhs.TimeStamp && lhs.Message == rhs.Message;
            /// <summary>
            /// Test two values for inequality
            /// </summary>
            /// <param name="lhs">left hand operand</param>
            /// <param name="rhs">right hand operand</param>
            /// <returns>true if values are not equal, false if they are equal</returns>
            public static bool operator !=(in OutputHelperContent lhs, in OutputHelperContent rhs) =>
                !(lhs == rhs);

            /// <inheritdoc />
            public bool Equals(OutputHelperContent other) => this == other;

            /// <inheritdoc />
            public override bool Equals(object other) => other is OutputHelperContent content && content == this;
            /// <summary>
            /// Test <paramref name="lhs"/> to see if it is greater than <paramref name="rhs"/>.
            /// </summary>
            /// <param name="lhs">left hand operand</param>
            /// <param name="rhs">right hand operand</param>
            /// <returns>true if <paramref name="lhs"/> is greater than <paramref name="rhs"/>, false otherwise</returns>
            public static bool operator >(in OutputHelperContent lhs, in OutputHelperContent rhs) =>
                Compare(in lhs, in rhs) > 0;
            /// <summary>
            /// Test <paramref name="lhs"/> to see if it is less than <paramref name="rhs"/>.
            /// </summary>
            /// <param name="lhs">left hand operand</param>
            /// <param name="rhs">right hand operand</param>
            /// <returns>true if <paramref name="lhs"/> is less than <paramref name="rhs"/>, false otherwise</returns>
            public static bool operator <(in OutputHelperContent lhs, in OutputHelperContent rhs) =>
                Compare(in lhs, in rhs) < 0;
            /// <summary>
            /// Test <paramref name="lhs"/> to see if it is greater than or equal to <paramref name="rhs"/>.
            /// </summary>
            /// <param name="lhs">left hand operand</param>
            /// <param name="rhs">right hand operand</param>
            /// <returns>true if <paramref name="lhs"/> is greater than or equal to <paramref name="rhs"/>, false otherwise</returns>
            public static bool operator >=(in OutputHelperContent lhs, in OutputHelperContent rhs) => !(lhs < rhs);
            /// <summary>
            /// Test <paramref name="lhs"/> to see if it is less than or equal to <paramref name="rhs"/>.
            /// </summary>
            /// <param name="lhs">left hand operand</param>
            /// <param name="rhs">right hand operand</param>
            /// <returns>true if <paramref name="lhs"/> is less than or equal to <paramref name="rhs"/>, false otherwise</returns>
            public static bool operator <=(in OutputHelperContent lhs, in OutputHelperContent rhs) => !(lhs > rhs);
            /// <summary>
            /// Compare this value with another value of same type
            /// </summary>
            /// <param name="other">the other value</param>
            /// <returns>0 if this value equals <paramref name="other"/>, a negative number if less than <paramref name="other"/>
            /// and a positive number if it is greater than <paramref name="other"/>.</returns>
            public int CompareTo(OutputHelperContent other) => Compare(in this, in other);

            /// <inheritdoc />
            public override int GetHashCode()
            {
                int hash = TimeStamp.GetHashCode();
                unchecked
                {
                    hash = (hash * 397) ^ Message.GetHashCode();
                }
                return hash;
            } 
            #endregion
        }
        #endregion

        #region Private Data

        [NotNull] private volatile Exception _threadStartException = null;
        [NotNull] private readonly Executor _eventThread;
        private SetOnceValFlag _threadTerminated = default;
        private SetOnceValFlag _threadStarted = default;
        private SetOnceValFlag _threadStartRequested = default;
        private SetOnceValFlag _disposed = default;
        [NotNull] private readonly LocklessConcreteType _concreteType;
        [NotNull] private readonly Thread _thread;
        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private readonly BlockingCollection<OutputHelperContent> _blockingCollection = new BlockingCollection<OutputHelperContent>(new ConcurrentQueue<OutputHelperContent>());
        [NotNull]
        private readonly MutableResourceVault<SortedSet<OutputHelperContent>> _resourceVault =
            MutableResourceVault<SortedSet<OutputHelperContent>>.CreateAtomicMutableResourceVault(
                () => new SortedSet<OutputHelperContent>(), TimeSpan.FromMilliseconds(250));
        private static long _instanceCount;
        [NotNull] private const string ThreadNamePrefix = "OrderedOutputHelper_{0}"; 
        #endregion
    }
}