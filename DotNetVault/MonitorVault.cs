using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.TimeStamps;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using TToggleFlag = DotNetVault.ToggleFlags.ToggleFlag;
namespace DotNetVault
{
    /// <summary>
    /// A base class for vaults that use monitor and an object
    /// as their synchronization mechanism
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class MonitorVault<T> : Vault<T>
    {

        internal static TimeSpan FallbackSleepInterval => TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// The sync object
        /// </summary>
        [NotNull] protected object SyncRoot => _syncObject;

        /// <inheritdoc />
        protected MonitorVault(TimeSpan defaultTimeout) : base(defaultTimeout)
        {
        }
        /// <summary>
        /// Passed in delegate to the locked resource object returned so it knows how to
        /// return the object to the vault when it goes out of scope
        /// </summary>
        /// <param name="v">the vault to which the object should be returned</param>
        /// <param name="b">the box that should be returned</param>
        /// <returns>should return null always</returns>
        [CanBeNull]
        protected internal static Box ReleaseResourceMethod<TAv>([NotNull] TAv v, [NotNull] Box b) where TAv : MonitorVault<T>
        {
            Debug.Assert(b != null && v != null && ReferenceEquals(v._lockedResource, b));
            Monitor.Exit(v._syncObject);            
            return null;
        }
        /// <inheritdoc />
        protected override void Dispose(bool disposing, TimeSpan? timeout = null)
        {
            if (disposing)
            {
                TimeSpan disposeTimeout = timeout ?? DisposeTimeout;
                if (disposeTimeout <= TimeSpan.Zero)
                {
                    disposeTimeout = DisposeTimeout;
                }
                Debug.Assert(disposeTimeout > TimeSpan.Zero);
                if (_disposeFlag.SignalDisposeBegin())
                {
                    try
                    {
                        using (var l = ExecuteGetInternalLockedResourceDuringDispose(disposeTimeout))
                        {
                            l.Destroy();
                            _lockedResource = null;
                            // ReSharper disable once RedundantAssignment
                            bool finishedDispose = _disposeFlag.SignalDisposed();
                            Debug.Assert(finishedDispose);
                        }
                    }
                    catch (TimeoutException)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        throw;
                    }
                    catch (LockAlreadyHeldThreadException e)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        Console.Error.WriteLine(e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        Console.Error.WriteLine(e);
                        throw new TimeoutException($"Unable to obtain lock within {disposeTimeout.TotalMilliseconds:F3} milliseconds.", e);
                    }

                }
            }
        }

        /// <summary>
        /// Try to get the locked resource.  This thread will block (potentially forever)
        /// until the resource is obtained or an exception is thrown.
        /// </summary>
        /// <returns>The locked resource</returns>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread attempting to obtain the lock,
        /// already holds the lock.</exception>
        protected MonitorLockedResource ExecuteGetInternalLockedResourceBlockForever()
        {
            ThrowIfDisposingOrDisposed();
            return MonitorLockedResource.CreateInternalLockedResourceBlockForever(this);
        }

        /// <summary>
        /// Try to get the resource until earliest of following happens
        ///     1- get it successfully,
        ///     2- cancellation requested via <paramref name="token"/>
        ///     3- time specified by <paramref name="timeout"/> exceeded
        /// </summary>
        /// <param name="timeout">how long should we wait?  Null indicates potential infinite wait .</param>
        /// <param name="token">token by which another thread can cancel the attempt to obtain resource</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException">non-null, non-positive <paramref name="timeout"/> argument</exception>
        /// <exception cref="TimeoutException">didn't obtain it within time specified by <paramref name="timeout"/></exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread attempting to obtain the lock already holds the lock.</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected MonitorLockedResource ExecuteGetInternalLockedResource(TimeSpan? timeout, CancellationToken token)
        {
            ThrowIfDisposingOrDisposed();
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Must be positive.");
            }
            return MonitorLockedResource.CreateInternalLockedResource(this, timeout, token);
        }

        private MonitorLockedResource ExecuteGetInternalLockedResourceDuringDispose(TimeSpan timeOut)
        {
            ThrowIfNotDisposing();
            if (timeOut <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut, @"Must be positive.");
            return MonitorLockedResource.CreateInternalLockedResource( this, timeOut, CancellationToken.None, true);
        }

        /// <summary>
        /// intermediate locked resource object storing protected resource after extracted from vault but
        /// before final delivery to user behind a method with the <see cref="UsingMandatoryAttribute"/>
        /// </summary>
        protected internal ref struct MonitorLockedResource
        {

            internal static MonitorLockedResource CreateInternalLockedResourceBlockForever<TV>([NotNull] TV owner, bool vaultDisposing = false) where TV : MonitorVault<T>
            {
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." +
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }

                var boxRes = AcquireBoxPointerPotentialWaitForever(owner);
                Debug.Assert((boxRes.Result == null ) != (boxRes.AlreadyAcquiredThreadId == null));
                try
                {
                    if (boxRes.AlreadyAcquiredThreadId != null)
                        throw new LockAlreadyHeldThreadException(boxRes.AlreadyAcquiredThreadId.Value);
                    return new MonitorLockedResource(owner, boxRes.Result);
                }
                catch (Exception)
                {
                    if (boxRes.Result != null)
                    {
                        Monitor.Exit(owner._syncObject);
                    }
                    throw;
                }

            }
            internal static MonitorLockedResource CreateInternalLockedResource<TV>([NotNull] TV owner, TimeSpan? timeout,
                CancellationToken token, bool vaultDisposing = false) where TV : MonitorVault<T>
            {
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (timeout == null && token == CancellationToken.None)
                    throw new ArgumentException("Cancellation token may not be none if no timeout is specified.");
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." +
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }
                var boxRes = AcquireBoxPointer(owner, timeout, token);
                try
                {
                    if (boxRes.Cancelled)
                        throw new OperationCanceledException(token);
                    if (boxRes.TimedOut)
                        throw new TimeoutException(
                            "Unable to acquire resource within " +
                            $"[{(timeout ?? owner.DefaultTimeout).TotalMilliseconds:F3}] milliseconds.");
                    if (boxRes.AlreadyAcquiredThreadId != null)
                        throw new LockAlreadyHeldThreadException(boxRes.AlreadyAcquiredThreadId.Value);
                    Debug.Assert(boxRes.AcquiredBox != null);
                    return new MonitorLockedResource(owner, boxRes.AcquiredBox);
                }
                catch (Exception)
                {
                    if (boxRes.AcquiredBox != null)
                    {
                        Monitor.Exit(owner._syncObject);
                    }
                    throw;
                }
            }

            /// <summary>
            /// This holds a valid resource
            /// </summary>
            public readonly bool IsGood => _isGood;

            /// <summary>
            /// a reference to the protected resource
            /// </summary>
            public ref T Value => ref _b.Value;


            private MonitorLockedResource([NotNull] MonitorVault<T> owner, [NotNull] Box b)
            {
                _b = b ?? throw new ArgumentNullException(nameof(b));
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _releaseFlag = new TToggleFlag(false);
                _isGood = true;
            }

            /// <summary>
            /// Creates a bad vault
            /// </summary>
            /// <param name="owner">the owner</param>
            // ReSharper disable once UnusedMember.Local
            private MonitorLockedResource([NotNull] MonitorVault<T> owner)
            {
                _releaseFlag = new TToggleFlag(false);
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _b = null;
                _isGood = false;
            }
            
            /// <summary>
            /// returns the resource called only during vault disposal
            /// </summary>
            public void Destroy()
            {
                Debug.Assert(_b != null && ReferenceEquals(_owner._lockedResource, _b));
                _b.Dispose();
                _owner._resourcePtr = null;
            }
            
            /// <summary>
            /// Release the protected resource
            /// </summary>
            public void Dispose() => Dispose(true);

            /// <summary>
            /// release the box to the next container on its way to the user
            /// </summary>
            /// <returns>the box</returns>
            /// <exception cref="InvalidOperationException">already released</exception>
            internal Box Release()
            {
                if (_releaseFlag.SetFlag())
                {
                    Box b = _b;
                    Debug.Assert(IsGood && b != null);
                    _b = null;
                    return b;
                }
                throw new InvalidOperationException("The box has already been released.");
            }

            private static (Box Result, int? AlreadyAcquiredThreadId) AcquireBoxPointerPotentialWaitForever<TVault>([NotNull] TVault owner)
                where TVault : MonitorVault<T>
            {
                Debug.Assert(owner._lockedResource != null);
                Box res = null;
                int? threadId =  Monitor.IsEntered(owner._syncObject)
                    ? (int?) Thread.CurrentThread.ManagedThreadId
                    : null;
                if (threadId == null)
                {
                    Monitor.Enter(owner._syncObject);
                    res = owner._lockedResource;
                }
                
                Debug.Assert((threadId == null) != (res == null));
                return (res, threadId);
            }

            private void Dispose(bool disposing)
            {
                if (disposing && _releaseFlag.SetFlag())
                {
                    if (IsGood)
                    {
                        Monitor.Exit(_owner._syncObject);
                    }
                }
                _b = null;
            }

            private static (Box AcquiredBox, bool Cancelled, bool TimedOut, int? AlreadyAcquiredThreadId) AcquireBoxPointer<TVault>([NotNull] TVault owner,
                TimeSpan? timeout, CancellationToken token) where TVault : MonitorVault<T>
            {
                // ReSharper disable once ConstantConditionalAccessQualifier
                Debug.Assert(owner?._lockedResource != null);
                Box acquiredPtr;
                bool cancel = false;
                bool timedOut;
                int? alreadyHeldThreadId;
                Debug.Assert(timeout != null || token != CancellationToken.None);

                if (Monitor.IsEntered(owner._syncObject))
                {
                    acquiredPtr = null;
                    timedOut = false;
                    alreadyHeldThreadId = Thread.CurrentThread.ManagedThreadId;
                }
                else
                {
                    alreadyHeldThreadId = null;
                    DateTime? quitAfter = DnvTimeStampProvider.MonoLocalNow + timeout;
                    TimeSpan ownerSleepInterval = owner.SleepInterval;
                    TimeSpan sleepFor = ownerSleepInterval > TimeSpan.Zero && ownerSleepInterval < timeout
                        ? ownerSleepInterval
                        : TimeSpan.FromMilliseconds(1);
                    bool gotLock = false;

                    try
                    {
                        if (token != CancellationToken.None)
                        {
                            while (!gotLock && !cancel && (quitAfter == null || DnvTimeStampProvider.MonoLocalNow <= quitAfter))
                            {
                                Monitor.TryEnter(owner._syncObject, sleepFor, ref gotLock);
                                if (!gotLock)
                                {
                                    cancel = token.IsCancellationRequested;
                                }
                            }
                        }
                        else
                        {
                            Monitor.TryEnter(owner._syncObject, timeout ?? owner.DefaultTimeout, ref gotLock);
                        }
                        acquiredPtr = gotLock ? owner._lockedResource : null;

                        if (acquiredPtr != null)
                        {
                            Debug.Assert(gotLock);
                            timedOut = false;
                        }
                        else
                        {
                            Debug.Assert(!gotLock);
                            timedOut = !cancel;
                            cancel = !timedOut;
                        }
                    }
                    catch (Exception)
                    {
                        if (gotLock)
                        {
                            Monitor.Exit(owner._syncObject);
                        }
                        throw;
                    }
                }

                Debug.Assert(DoPostConditionCheck(acquiredPtr, cancel, timedOut, alreadyHeldThreadId));
                return (acquiredPtr, cancel, timedOut, alreadyHeldThreadId);

                static bool DoPostConditionCheck(Box b, bool c, bool to, int? thrd)
                {
                    bool gotTheBox = b != null;
                    bool weCancelled = c;
                    bool weTimedOut = to;
                    return gotTheBox ?
                        !weCancelled && !weTimedOut //since we got it, we didn't cancel and there wasn't a timeout
                        : ( (weCancelled != weTimedOut && thrd == null) || (!weCancelled && thrd != null)); //we didn't get it, we cancelled xor we timed out (not both) 
                }
            }


            private readonly bool _isGood;
            private readonly TToggleFlag _releaseFlag;
            private Box _b;
            [NotNull] private readonly MonitorVault<T> _owner;
        }

        [NotNull] private protected readonly object _syncObject = new object();
        
    }
}
