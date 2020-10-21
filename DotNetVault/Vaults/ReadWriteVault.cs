using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using JetBrains.Annotations;
using Locker = System.Threading.ReaderWriterLockSlim;
using TToggleFlag = DotNetVault.Logging.SetOnceValFlag;
namespace DotNetVault.Vaults
{
    /// <summary>
    /// A base class for vaults that provide shared read locks as well as write locks
    /// </summary>
    /// <typeparam name="T">The protected resource type.</typeparam>
    public abstract class ReadWriteVault<T> : Vault<T>
    {
        internal static TimeSpan FallbackSleepInterval => TimeSpan.FromMilliseconds(10);
        
        /// <summary>
        /// Release the resource
        /// </summary>
        /// <param name="v">the vault to which the resource should be returned</param>
        /// <param name="b">the box of the resource to be returned</param>
        /// <param name="mode">the mode in which the lock was obtained</param>
        /// <typeparam name="TAv">The type of ReadWriteVault</typeparam>
        /// <returns>null</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> was not a defined value of
        /// the <see cref="AcquisitionMode"/> <see langword="enum"/></exception>
        protected internal static Box ReleaseResourceMethod<TAv>([NotNull] TAv v, Box b, AcquisitionMode mode) where TAv : ReadWriteVault<T>
        {
            // ReSharper disable once ConstantConditionalAccessQualifier
            Debug.Assert(b != null && v?._locker != null && 
                         ReferenceEquals(v._lockedResource, b));
            var locker = v._locker;
            switch (mode)
            {
                case AcquisitionMode.ReadOnly:
                    locker.ExitReadLock();
                    break;
                case AcquisitionMode.UpgradableReadOnly:
                    locker.ExitUpgradeableReadLock();
                    break;
                case AcquisitionMode.ReadWrite:
                    locker.ExitWriteLock();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), 
                        mode, null);
            }
            return null;
        }

        /// <summary>
        /// The sync object.
        /// </summary>
        [NotNull] protected Locker SyncRoot => _locker;
        /// <summary>
        /// True only if the current thread already holds read lock
        /// </summary>
        protected bool ThisThreadHasReadLock => _locker.IsReadLockHeld;
        /// <summary>
        /// True only if the current thread already holds upgradable read lock
        /// </summary>
        protected bool ThisThreadHasUpgradeableReadLock => _locker.IsUpgradeableReadLockHeld;
        /// <summary>
        /// True only if the current thread already holds write lock
        /// </summary>
        protected bool ThisThreadHasWriteLock => _locker.IsWriteLockHeld;
        /// <summary>
        /// True only if current thread already hold any kind of lock.
        /// </summary>
        protected bool ThisThreadHoldAnyLock =>
            ThisThreadHasWriteLock || ThisThreadHasReadLock || ThisThreadHasUpgradeableReadLock;
        
        /// <summary>
        /// CTOR -- create a vault
        /// </summary>
        /// <param name="defaultTimeout">the amount of time to wait when trying to get lock
        /// when no timeout is supplied to lock methods. </param>
        protected ReadWriteVault(TimeSpan defaultTimeout) : this(defaultTimeout,
            DefaultLockerInit){}

        private protected ReadWriteVault(TimeSpan defaultTimeout, [NotNull] Func<Locker> lockCtor) : base(
            defaultTimeout) 
        {
            if (lockCtor == null) throw new ArgumentNullException(nameof(lockCtor));
            try
            {
                _locker = lockCtor();
            }
            catch (Exception ex)
            {
                throw new DelegateThrewException<Func<Locker>>(lockCtor, nameof(lockCtor), ex);
            }
        }

        /// <inheritdoc />
        protected sealed override void Dispose(bool disposing, TimeSpan? timeout = null)
        {
            ExecuteDispose(disposing, timeout);
            if (disposing && _flag.TrySet())
            {
                _locker.Dispose();
            }
            _flag.TrySet();
        }

        /// <summary>
        /// Implement disposal logic here
        /// </summary>
        /// <param name="disposing">called by managed code -> true, called by finalizer -> false</param>
        /// <param name="timeout">how long to wait</param>
        protected abstract void ExecuteDispose(bool disposing, TimeSpan? timeout = null);

        /// <summary>
        /// Try to get the locked resource.  This thread will block (potentially forever)
        /// until the resource is obtained or an exception is thrown.
        /// </summary>
        /// <returns>The locked resource</returns>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread attempting to obtain the lock,
        /// already holds the lock.</exception>
        protected RwVaultInternalLockedResource ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode mode)
        {
            ThrowIfDisposingOrDisposed();
            return RwVaultInternalLockedResource.CreateInternalLockedResourceBlockForever(this, mode.ValueOrThrowIfNDef());
        }
        /// <summary>
        /// Get the protected resource in writable mode during execution of the dispose routine
        /// </summary>
        /// <param name="timeOut">the timeout</param>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">unable to secure resource within period specified by the
        /// <paramref name="timeOut"/> parameter.</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the lock is already held by the calling thread</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeOut"/> was not positive.</exception>
        protected RwVaultInternalLockedResource ExecuteGetInternalLockedResourceDuringDispose(TimeSpan timeOut)
        {
            ThrowIfNotDisposing();
            if (timeOut <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut, @"Must be positive.");
            return RwVaultInternalLockedResource.CreateInternalLockedResource(this, timeOut, CancellationToken.None,
                AcquisitionMode.ReadWrite, true);
        }
        /// <summary>
        /// Try to get the resource until earliest of following happens
        ///     1- get it successfully,
        ///     2- cancellation requested via <paramref name="token"/>
        ///     3- time specified by <paramref name="timeout"/> exceeded
        /// </summary>
        /// <param name="timeout">how long should we wait?  Null indicates potential infinite wait .</param>
        /// <param name="token">token by which another thread can cancel the attempt to obtain resource</param>
        /// <param name="mode">acquisition mode</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException">non-null, non-positive <paramref name="timeout"/> argument; OR mode not
        /// a defined value of the <see cref="AcquisitionMode"/> <see langword="enum"/></exception>
        /// <exception cref="TimeoutException">didn't obtain it within time specified by <paramref name="timeout"/></exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread attempting to obtain the lock already holds the lock.</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected RwVaultInternalLockedResource ExecuteGetInternalLockedResource(TimeSpan? timeout, CancellationToken token, AcquisitionMode mode)
        {
            ThrowIfDisposingOrDisposed();
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Must be positive.");
            }
            return RwVaultInternalLockedResource.CreateInternalLockedResource(this, timeout, token, mode.ValueOrThrowIfNDef());
        }

        /// <summary>
        /// Internal read write lock protected resource
        /// </summary>
        protected internal ref struct RwVaultInternalLockedResource
        {

            [SuppressMessage("ReSharper", "RedundantLambdaParameterType")]
            internal static RwVaultInternalLockedResource CreateInternalLockedResourceBlockForever<TV>([NotNull] TV owner, AcquisitionMode mode, bool vaultDisposing = false) where TV : ReadWriteVault<T>
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

                Box boxRes = null;
                try
                {
                    boxRes = AcquireBoxPointerPotentiallyWaitForever(owner, mode);
                    Debug.Assert(boxRes != null);
                    (Action<TimeSpan?, CancellationToken> upgradeAction, Action upgradeForeverAction) =
                        GetUpgradeActions(mode);
                    Debug.Assert((upgradeForeverAction == null) == (upgradeAction == null));
                    return new RwVaultInternalLockedResource(owner, boxRes, upgradeAction, upgradeForeverAction, mode);
                }
                catch (LockRecursionException ex)
                {
                    Debug.Assert(boxRes == null);
                    throw new RwLockAlreadyHeldThreadException(Thread.CurrentThread.ManagedThreadId, ex);
                }
                catch (Exception)
                {
                    if (boxRes != null)
                    {
                        ReleaseLock(owner._locker, mode);
                    }
                    throw;
                }

                (Action<TimeSpan?, CancellationToken> UpgradeAction, Action UpgradeForeverAction) GetUpgradeActions(AcquisitionMode m)
                {
                    return m == AcquisitionMode.UpgradableReadOnly
                        ? (
                            (TimeSpan? ts, CancellationToken tkn) =>
                                Upgrade(ts, tkn), () => UpgradeForever()) : (NullUpgradeAction, NullUpgradeForeverAction);
                }



                void Upgrade(TimeSpan? ts, CancellationToken tkn) =>
                    CreateInternalLockedResource(owner, ts, tkn, AcquisitionMode.ReadWrite);

                void UpgradeForever() => CreateInternalLockedResourceBlockForever(owner, AcquisitionMode.ReadWrite);
            }

            [SuppressMessage("ReSharper", "RedundantLambdaParameterType")]
            internal static RwVaultInternalLockedResource 
                CreateInternalLockedResource<TV>([NotNull] TV owner, TimeSpan? timeout,
           CancellationToken token, AcquisitionMode mode, bool vaultDisposing = false) where TV : ReadWriteVault<T>
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
                mode = mode.ValueOrThrowIfNDef();
                (Box AcquiredBox, bool Cancelled, bool TimedOut) boxRes = default;
                try
                {
                    boxRes = AcquireBoxPointer(owner, timeout, mode, token);
                    if (boxRes.Cancelled)
                        throw new OperationCanceledException(token);
                    if (boxRes.TimedOut)
                        throw new TimeoutException(
                            "Unable to acquire resource within " +
                            $"[{(timeout ?? owner.DefaultTimeout).TotalMilliseconds:F3}] milliseconds.");
                    (Action<TimeSpan?, CancellationToken> upgradeAction, Action upgradeForeverAction) =
                        GetUpgradeActions(mode);
                    Debug.Assert((upgradeForeverAction == null) == (upgradeAction == null));
                    
                    return new RwVaultInternalLockedResource(owner, boxRes.AcquiredBox, upgradeAction, upgradeForeverAction, mode); 
                }
                catch (LockRecursionException ex)
                {
                    Debug.Assert(boxRes.AcquiredBox == null);
                    throw new RwLockAlreadyHeldThreadException(Thread.CurrentThread.ManagedThreadId, ex);
                }
                catch (Exception)
                {
                    if (boxRes.AcquiredBox != null)
                    {
                        ReleaseLock(owner._locker, mode);
                    }
                    throw;
                }

                (Action<TimeSpan?, CancellationToken> UpgradeAction, Action UpgradeForeverAction) GetUpgradeActions(AcquisitionMode m)
                {
                    return m == AcquisitionMode.UpgradableReadOnly
                        ? (
                            (TimeSpan? ts, CancellationToken tkn) =>
                                Upgrade(ts, tkn), () => UpgradeForever()) : (NullUpgradeAction, NullUpgradeForeverAction);
                }

                

                void Upgrade(TimeSpan? ts, CancellationToken tkn) =>
                    CreateInternalLockedResource(owner, ts, tkn, AcquisitionMode.ReadWrite);

                void UpgradeForever() => CreateInternalLockedResourceBlockForever(owner, AcquisitionMode.ReadWrite);
            }

            /// <summary>
            /// This holds a valid resource
            /// </summary>
            public readonly bool IsGood => _isGood;

            /// <summary>
            /// True if acquired as an upgradable read lock, false otherwise
            /// </summary>
            public readonly bool IsUpgradable => _upgradeAction != null;

            /// <summary>
            /// a readonly reference to the protected resource 
            /// </summary>
            public readonly ref T Value => ref _b.Value;

            [CanBeNull] internal Action<TimeSpan?, CancellationToken> UpgradeAction => _upgradeAction;

            [CanBeNull] internal Action UpgradePotentialWaitForeverAction => _upgradeForeverAction;

            private RwVaultInternalLockedResource([NotNull] ReadWriteVault<T> owner, [NotNull] Box b, [CanBeNull] Action<TimeSpan?, CancellationToken> upgradeAction, [CanBeNull] Action upgradeForeverAction, AcquisitionMode mode)
            {
                _mode = mode.ValueOrThrowIfNDef();
                _b = b ?? throw new ArgumentNullException(nameof(b));
                _upgradeAction = upgradeAction;
                _upgradeForeverAction = upgradeForeverAction;
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _releaseFlag = default;
                _isGood = true;
                Debug.Assert((_upgradeAction == null) == (_upgradeForeverAction == null),
                    "Both ok, neither ok.  Exactly one not ok.");
            }

            /// <summary>
            /// Creates a bad vault
            /// </summary>
            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            private RwVaultInternalLockedResource([NotNull] ReadWriteVault<T> owner)
            {
                _releaseFlag = default;
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _upgradeAction = null;
                _upgradeForeverAction = null;
                _b = null;
                _isGood = false;
                _mode = AcquisitionMode.ReadOnly;
                Debug.Assert((_upgradeAction == null) == (_upgradeForeverAction == null),
                    "Both ok, neither ok.  Exactly one not ok.");
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

            private void Dispose(bool disposing)
            {
                Debug.Assert(_mode.IsDefined());
                if (disposing && _releaseFlag.TrySet())
                {
                    if (IsGood)
                    {
                        switch (_mode)
                        {
                            default:
                            case AcquisitionMode.ReadOnly:
                                _owner.SyncRoot.ExitReadLock();
                                break;
                            case AcquisitionMode.UpgradableReadOnly:
                                _owner.SyncRoot.ExitUpgradeableReadLock();
                                break;
                            case AcquisitionMode.ReadWrite:
                                _owner.SyncRoot.ExitWriteLock();
                                break;
                        }
                    }
                }
                _b = null;
            }
            /// <summary>
            /// release the box to the next container on its way to the user
            /// </summary>
            /// <returns>the box</returns>
            /// <exception cref="InvalidOperationException">already released</exception>
            internal Box Release()
            {
                if (_releaseFlag.TrySet())
                {
                    Box b = _b;
                    Debug.Assert(IsGood && b != null);
                    _b = null;
                    return b;
                }
                throw new InvalidOperationException("The box has already been released.");
            }

            internal (Box Box, Action<TimeSpan?, CancellationToken> AcquireAction, Action AcquireForeverAction) ReleaseUpgradable()
            {
                if (_upgradeAction == null || _upgradeForeverAction == null) throw new InvalidOperationException("The lock is not upgradable");
                if (_releaseFlag.TrySet())
                {
                    Box b = _b;
                    Action<TimeSpan?, CancellationToken> acqAction = _upgradeAction;
                    Action acqForever = _upgradeForeverAction;
                    Debug.Assert(IsGood && b != null);
                    _b = null;
                    return (b, acqAction, acqForever);
                }
                throw new InvalidOperationException("The box has already been released.");
            }

            private static Box AcquireBoxPointerPotentiallyWaitForever<TVault>([NotNull] TVault owner, AcquisitionMode mode)
                where TVault : ReadWriteVault<T>
            {
                // ReSharper disable once ConstantConditionalAccessQualifier
                Debug.Assert(owner?._lockedResource != null && owner._locker != null);
                Debug.Assert(mode == AcquisitionMode.UpgradableReadOnly || mode == AcquisitionMode.ReadWrite ||
                             mode == AcquisitionMode.ReadOnly);
                
                var locker = owner._locker;
                switch (mode)
                {
                    default:
                    case AcquisitionMode.ReadOnly:
                        locker.EnterReadLock();
                        break;
                    case AcquisitionMode.UpgradableReadOnly:
                        locker.EnterUpgradeableReadLock();
                        break;
                    case AcquisitionMode.ReadWrite:
                        locker.EnterWriteLock();
                        break;
                }

                return owner._lockedResource;
            }

            private static (Box AcquiredBox, bool Cancelled, bool TimedOut) AcquireBoxPointer<TVault>(
                [NotNull] TVault owner,
                TimeSpan? timeout, AcquisitionMode mode, CancellationToken token) where TVault : ReadWriteVault<T>
            {
                // ReSharper disable once ConstantConditionalAccessQualifier
                Debug.Assert(owner._locker != null);
                Locker locker = owner._locker;
                Box acquiredPtr;
                bool cancel = false;
                bool timedOut;

                Debug.Assert(timeout != null || token != CancellationToken.None);


                DateTime? quitAfter = DateTime.Now + timeout;
                TimeSpan ownerSleepInterval = owner.SleepInterval;
                TimeSpan sleepFor = ownerSleepInterval > TimeSpan.Zero && ownerSleepInterval < timeout
                    ? ownerSleepInterval
                    : TimeSpan.FromMilliseconds(1);
                bool gotLock = false;
                try
                {
                    if (token != CancellationToken.None)
                    {
                        while (!gotLock && !cancel && (quitAfter == null || DateTime.Now <= quitAfter))
                        {
                            gotLock = EnterLock(mode, locker, sleepFor);
                            if (!gotLock)
                            {
                                cancel = token.IsCancellationRequested;
                            }
                        }
                    }
                    else
                    {
                        gotLock = EnterLock(mode, locker, timeout ?? owner.DefaultTimeout);
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
                catch (LockRecursionException ex)
                {
                    throw new RwLockAlreadyHeldThreadException(Thread.CurrentThread.ManagedThreadId, ex);
                }
                catch (Exception)
                {
                    if (gotLock)
                    {
                        ReleaseLock(locker, mode);
                    }
                    throw;
                }


                Debug.Assert(DoPostConditionCheck(acquiredPtr, cancel, timedOut));
                return (acquiredPtr, cancel, timedOut);

                static bool EnterLock(AcquisitionMode mode, Locker l, TimeSpan limit)
                {
                    Debug.Assert(l != null && limit > TimeSpan.Zero);
                    Debug.Assert(mode == AcquisitionMode.ReadOnly || mode == AcquisitionMode.ReadWrite ||
                                 mode == AcquisitionMode.UpgradableReadOnly);
                    bool ret;
                    switch (mode)
                    {
                        default:
                        case AcquisitionMode.ReadOnly:
                            ret = l.TryEnterReadLock(limit);
                            break;
                        case AcquisitionMode.UpgradableReadOnly:
                            ret = l.TryEnterUpgradeableReadLock(limit);
                            break;
                        case AcquisitionMode.ReadWrite:
                            ret = l.TryEnterWriteLock(limit);
                            break;
                    }
                    return ret;
                }

                static bool DoPostConditionCheck(Box b, bool c, bool to)
                {
                    bool gotTheBox = b != null;
                    bool weCancelled = c;
                    bool weTimedOut = to;
                    return gotTheBox
                        ? !weCancelled && !weTimedOut //since we got it, we didn't cancel and there wasn't a timeout
                        : weCancelled != weTimedOut; //we didn't get it, we cancelled xor we timed out (not both)
                }

            }

            static void ReleaseLock(Locker l, AcquisitionMode m)
            {
                Debug.Assert(l != null && m.IsDefined());
                switch (m)
                {
                    default:
                    case AcquisitionMode.ReadOnly:
                        l.ExitReadLock();
                        break;
                    case AcquisitionMode.UpgradableReadOnly:
                        l.ExitUpgradeableReadLock();
                        break;
                    case AcquisitionMode.ReadWrite:
                        l.ExitWriteLock();
                        break;
                }
            }

            private readonly AcquisitionMode _mode;
            [CanBeNull] private readonly Action<TimeSpan?, CancellationToken> _upgradeAction;
            private readonly bool _isGood;
            private TToggleFlag _releaseFlag;
            private Box _b;
            [NotNull] private readonly ReadWriteVault<T> _owner;
            [CanBeNull] private Action _upgradeForeverAction;
            private const Action<TimeSpan?, CancellationToken> NullUpgradeAction = null;
            private const Action NullUpgradeForeverAction = null;
        }

       

        private static Locker DefaultLockerInit() => new Locker(LockRecursionPolicy.NoRecursion);
        [NotNull] private readonly Locker _locker;
        private TToggleFlag _flag = default;
    }

    /// <summary>
    /// The mode in which the rw lock should be or was obtained
    /// </summary>
    public enum AcquisitionMode
    {
        /// <summary>
        /// Read only lock
        /// </summary>
        ReadOnly,
        /// <summary>
        /// Upgradable read only lock
        /// </summary>
        UpgradableReadOnly,
        /// <summary>
        /// Read write lock
        /// </summary>
        ReadWrite,
    }

    internal static class AcquisitionModeExtensions
    {
        public static bool IsDefined(this AcquisitionMode m) => AllTheModes.Contains(m);

        public static AcquisitionMode ValueOrThrowIfNDef(this AcquisitionMode m) => m.IsDefined()
            ? m
            : throw new ArgumentOutOfRangeException(nameof(m), m,
                @"Parameter is not a value defined by the " +
                $@"{typeof(AcquisitionMode).Name} enumeration.");

        private static readonly ImmutableArray<AcquisitionMode> AllTheModes =
            Enum.GetValues(typeof(AcquisitionMode)).Cast<AcquisitionMode>().ToImmutableArray();
    }
}
