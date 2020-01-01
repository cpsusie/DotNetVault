using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    internal sealed class EventRaisingThread : IEventRaiser
    {
       public static IEventRaiser CreateEventRaiser([NotNull] string name)
            => new EventRaisingThread(name.ThrowIfNullEmptyOrWhitespace(nameof(name)));
        

        public event EventHandler<DelegateExceptionEventArgs> HandlerThrew;
        public bool IsDisposed => _disposed.IsSet;
        public bool ThreadActive => _flag.Code != ThreadStatusFlagCode.ThreadTerminated;
        private EventRaisingThread([NotNull] string threadName)
        {
            if (threadName == null) throw new ArgumentNullException(nameof(threadName));
            if (_flag.TrySetInstantiated() && _flag.TrySetRequestedThreadStart())
            {
                var thread = new Thread(Loop);
                thread.Name = threadName;
                thread.Priority = ThreadPriority.BelowNormal;
                //_thread.IsBackground = true;
                thread.Start(_cts.Token);
            }

            DateTime cancelAfter = TimeStampSource.Now + TimeSpan.FromSeconds(1);
            while (_flag.Code == ThreadStatusFlagCode.RequestedThreadStart && TimeStampSource.Now <= cancelAfter)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }

            if (_flag.Code == ThreadStatusFlagCode.RequestedThreadStart)
            {
                try
                {
                    _flag.ForceTerminate();
                    _disposed.TrySet();
                    _cts.Cancel();
                    _cts.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }
                throw new InvalidOperationException("Unable to confirm thread start.");
            }
        }

        public void Dispose() => Dispose(true);
        private void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                if (ThreadActive)
                {
                    try
                    {
                        _cts.Cancel();
                    }
                    catch (Exception)
                    {
                        //ignore
                    }
                }

                try
                {
                    _cts.Dispose();
                }
                catch (Exception)
                {
                    //ignore
                }

                try
                {
                    _collection.Dispose();
                }
                catch (Exception)
                {       
                    //ignore
                }

                HandlerThrew = null;
            }

            _disposed.TrySet();
        }

        public void AddAction(Action item)
        {
            if (IsDisposed || !ThreadActive)
            {
                throw new InvalidOperationException("Object is disposed or in a faulted state.");
            }
            if (item == null) throw new ArgumentNullException(nameof(item));
            _collection.Add(item);
        }

        private void Loop(object tokenObj)
        {
            bool setIt = _flag.TrySetThreadStarted();
            if (setIt && tokenObj is CancellationToken token)
            {
                try
                {
                    while (true)
                    {

                        token.ThrowIfCancellationRequested();
                        Action executeMe = DequeueAction(token);
                        if (executeMe != null)
                        {
                            try
                            {
                                executeMe();
                            }
                            catch (ObjectDisposedException)
                            {
                                throw;
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception e)
                            {
                                OnHandlerThrew(executeMe, e);
                            }
                        }
                    }
                                        
                }
                catch (OperationCanceledException)
                {
                    if (_flag.TrySetThreadTerminated())
                    {
                        Debug.WriteLine("Event raising thread terminating normally.");
                    }
                }
                finally
                {
                    _flag.ForceTerminate();
                }
            }
            else
            {
                BreakIfDebuggerAttached();
            }
            _flag.ForceTerminate();
        }
            


        
        

        [Conditional("DEBUG")]
        private void BreakIfDebuggerAttached()
        {
            if (Debugger.IsAttached)
                Debugger.Break();
        }
        private void OnHandlerThrew([NotNull] Action a, [NotNull] Exception thrown)
        {
            EventHandler<DelegateExceptionEventArgs> handler = HandlerThrew;
            if (handler != null)
            {
                DelegateExceptionEventArgs args = new DelegateExceptionEventArgs(nameof(a), thrown);
                handler(this, args);
            }
        }

        [CanBeNull]
        private Action DequeueAction(CancellationToken token)
        {
            bool gotSome = _collection.TryTake(out Action item, Timeout.Infinite, token);
            return gotSome ? item : null;
        }
        
        
        private readonly LocklessSetOnlyFlag _disposed = new LocklessSetOnlyFlag();
        private ThreadStatusFlag _flag = new ThreadStatusFlag();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly BlockingCollection<Action> _collection = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
    }

    public static class StringExtensions
    {
        public static string ThrowIfNullOrEmpty(this string s, string paramName)
        {
            if (s == null) throw new ArgumentNullException(paramName ?? nameof(paramName));
            if (string.IsNullOrEmpty(s))
                throw new ArgumentException(@"The string may not be empty.", paramName ?? nameof(paramName));
            return s;
        }

        public static string ThrowIfNullEmptyOrWhitespace(this string s, string paramName)
        {
            if (s == null) throw new ArgumentNullException(paramName ?? nameof(paramName));
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException(@"The string may not be empty or just whitespace.",
                    paramName ?? nameof(paramName));
            return s;
        }
    }
}