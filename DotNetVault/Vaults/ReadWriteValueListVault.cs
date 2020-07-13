using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using DotNetVault.LockedResources;
using DotNetVault.RefReturningCollections;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// A vault that protects a collection similar in but not identical in API to <see cref="System.Collections.Generic.List{T}"/>.
    /// The protected list is optimized for efficient holding and retrieval of LARGE value types -- it uses ref returns and is ref enumerable.
    ///
    /// It also provides sort and find functions that accept struct-based compares that compare parameters received by read-only reference.
    /// </summary>
    /// <typeparam name="TItem">The type of item held in the protected list.  It must be a vault-safe
    /// value type that is <see cref="IEquatable{T}"/> and <see cref="IComparable{T}"/></typeparam>
    public sealed class ReadWriteValueListVault<[VaultSafeTypeParam] TItem> 
        : ReadWriteListVault<TItem, BigValueList<TItem>>
            where TItem : struct, IEquatable<TItem>, IComparable<TItem>
    {
        #region CTORS
        /// <summary>
        /// Create a list vault
        /// </summary>
        /// <param name="items">the items to go in the list</param>
        /// <param name="capacity">the capacity of the new list (will be ignored if not greater than capacity of list after copying in items from <paramref name="items"/>.
        /// </param>
        /// <param name="timeout">default timeout period</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> or <paramref name="timeout"/> was negative</exception>
        public ReadWriteValueListVault([NotNull] VsEnumerableWrapper<TItem> items, int capacity, TimeSpan timeout) : base(
            CreateBigValueList(items ?? throw new ArgumentNullException(nameof(items)), capacity),
            timeout) { }
        /// <summary>
        /// Create a list vault
        /// </summary>
        /// <param name="items">the items to go in the list</param>
        /// <param name="timeout">default timeout period</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was negative</exception>
        public ReadWriteValueListVault([NotNull] VsEnumerableWrapper<TItem> items, TimeSpan timeout)
            : base(CreateBigValueList(items ?? throw new ArgumentNullException(nameof(items)),
                null), timeout) { }
        /// <summary>
        /// Create a list vault
        /// </summary>
        /// <param name="items">the items to go in the list</param>
        /// <param name="capacity">the capacity of the new list (will be ignored if not greater than capacity of list after copying in items from <paramref name="items"/>.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/>  was negative</exception>
        public ReadWriteValueListVault([NotNull] VsEnumerableWrapper<TItem> items, int capacity) : base(
            CreateBigValueList(items ?? throw new ArgumentNullException(nameof(items)), capacity),
            FallbackTimeout) { }
        /// <summary>
        /// Create a list vault
        /// </summary>
        /// <param name="items">the items to go in the list</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> was null.</exception>
        public ReadWriteValueListVault([NotNull] VsEnumerableWrapper<TItem> items) : base(
            CreateBigValueList(items ?? throw new ArgumentNullException(nameof(items)), null),
            FallbackTimeout) { }
        /// <summary>
        /// Create a list vault
        /// </summary>
        /// <param name="capacity">the storage capacity initially reserved for items</param>
        /// <param name="timeout">default timeout</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> or <paramref name="timeout"/> was negative</exception>
        public ReadWriteValueListVault(int capacity, TimeSpan timeout) : base(
            CreateBigValueList(null, capacity), timeout) { }
        /// <summary>
        /// Create a list vault
        /// </summary>
        /// <param name="capacity">the storage capacity initially reserved for items</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> was negative</exception>
        public ReadWriteValueListVault(int capacity) : base(
            CreateBigValueList(null, capacity), FallbackTimeout) { }
        /// <summary>
        /// Create a list vault
        /// </summary>
        /// <param name="timeout">default timeout</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was negative</exception>
        public ReadWriteValueListVault(TimeSpan timeout)
            : base(CreateBigValueList(null, null), timeout) { }
        /// <summary>
        /// Create a list vault
        /// </summary>
        public ReadWriteValueListVault()
            : base(CreateBigValueList(null, null), FallbackTimeout) { }
        #endregion

        #region Convenience Methods
        /// <summary>
        /// Get the current count of items
        /// </summary>
        /// <param name="timeout">how long to wait -- if null, <see cref="Vault{T}.DefaultTimeout"/> will be used.</param>
        /// <returns>the current count</returns>
        /// <remarks>Limited use -- because as soon as it returns there is no guarantee it will not have changed.</remarks>
        /// <remarks>Acquires and releases a readonly lock.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">timeout specified but not positive.</exception>
        public int GetCurrentCount(TimeSpan? timeout = null)
        {
            using var lck = RoLock(timeout ?? DefaultTimeout);
            return lck.Count;
        }

        /// <summary>
        /// Copies contents to array.
        /// </summary>
        /// <param name="timeout">how long to wait -- if null, <see cref="Vault{T}.DefaultTimeout"/> will be used.</param>
        /// <returns>the contents copied to an array.</returns>
        /// <remarks>Acquires and releases a readonly lock.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">timeout specified but not positive.</exception>
        public ImmutableArray<TItem> CopyContentsToArray(TimeSpan? timeout = null)
        {
            using var lck = RoLock(timeout ?? DefaultTimeout);
            return lck.ToArray();
        }

        /// <summary>
        /// Dumps contents to array, clearing itself.
        /// </summary>
        /// <param name="trimExcess">False by default.  If true, after it dumps contents, will trim excess allocated memory</param>
        /// <param name="timeout">how long to wait -- if null, <see cref="Vault{T}.DefaultTimeout"/> will be used.</param>
        /// <returns>the contents copied to an array.</returns>
        /// <remarks>Acquires and releases a readwrite lock.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">timeout specified but not positive.</exception>
        public ImmutableArray<TItem> DumpContentsToArrayAndClear(TimeSpan? timeout = null, bool trimExcess = false)
        {
            using var lck = Lock(timeout ?? DefaultTimeout);
            ImmutableArray<TItem> arr = lck.ToArray();
            lck.Clear();
            if (trimExcess)
            {
                lck.TrimExcess();
            }
            return arr;
        }
        #endregion

        #region Readonly Lock acquisition methods
        /// <summary>
        ///  Obtain a read-only resource.  Keep attempting until
        ///  sooner of following occurs:
        ///     1- time period specified by <paramref name="timeout"/> expires or
        ///     2- cancellation is requested via <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <param name="timeout">the max time to wait for</param>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> RoLock(TimeSpan timeout, CancellationToken token) =>
            PerformRoLock(timeout, token);

        /// <summary>
        ///  Obtain a read-only locked resource.   Yielding, (not busy), wait.  Keep attempting until
        ///  cancellation is requested via the <paramref name="token"/> parameter's
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> RoLock(CancellationToken token) =>
            PerformRoLock(null, token);

        /// <summary>
        /// Obtain a read-only locked resource.   Yielding, (not busy), wait.
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> RoLock(TimeSpan timeout) => PerformRoLock(timeout, CancellationToken.None);

        /// <summary>
        /// Obtain a read-only locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> RoLock() => PerformRoLock(DefaultTimeout, CancellationToken.None);

        /// <summary>
        /// Obtain a read-only locked resource.   This call can potentially block forever, unlike the
        /// other methods this vault exposes.  It may sometimes be desirable from a performance perspective
        /// not to check every so often for time expiration or cancellation requests.  For that reason, this explicitly
        /// named method exists.  Using this method, however, may cause a dead lock under certain circumstances (e.g.,
        /// you acquire this lock and another lock in different orders on different threads)
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> RoLockBlockUntilAcquired()
            => PerformRoLockBlockForever();
        #endregion

        #region Writable Lock acquisition methods
        /// <summary>
        ///  Obtain a writable locked resource.  Keep attempting until
        ///  sooner of following occurs:
        ///     1- time period specified by <paramref name="timeout"/> expires or
        ///     2- cancellation is requested via <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <param name="timeout">the max time to wait for</param>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> Lock(TimeSpan timeout, CancellationToken token) =>
            PerformWriteLock(timeout, token);

        /// <summary>
        ///  Obtain a writable locked resource.   Yielding, (not busy), wait.  Keep attempting until
        ///  cancellation is requested via the <paramref name="token"/> parameter's
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> Lock(CancellationToken token) =>
            PerformWriteLock(null, token);

        /// <summary>
        /// Obtain a writable locked resource.   Yielding, (not busy), wait.
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> Lock(TimeSpan timeout) => PerformWriteLock(timeout, CancellationToken.None);

        /// <summary>
        /// Obtain a writable locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> Lock() => PerformWriteLock(DefaultTimeout, CancellationToken.None);

        /// <summary>
        /// Obtain a writable locked resource.   This call can potentially block forever, unlike the
        /// other methods this vault exposes.  It may sometimes be desireable from a performance perspective
        /// not to check every so often for time expiration or cancellation requests.  For that reason, this explicitly
        /// named method exists.  Using this method, however, may cause a dead lock under certain circumstances (e.g.,
        /// you acquire this lock and another lock in different orders on different threads)
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> LockBlockUntilAcquired()
            => PerformWriteLockBlockForever();
        #endregion

        #region Spin Methods to allow source code compatibility when switching types of vaults.
        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(TimeSpan,CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> SpinLock(TimeSpan timeout, CancellationToken token) =>
            PerformWriteLock(timeout, token);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> SpinLock(CancellationToken token) =>
            PerformWriteLock(null, token);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(TimeSpan)"/>
        /// </summary>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> SpinLock(TimeSpan timeout) => PerformWriteLock(timeout, CancellationToken.None);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock()"/>
        /// </summary>
        [return: UsingMandatory]
        public ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> SpinLock() => PerformWriteLock(DefaultTimeout, CancellationToken.None);
        #endregion

        #region Upgradable ReadOnly Lock acquisition methods
        /// <summary>
        ///  Obtain an upgradable read-only resource.  Only one thread may have an upgradable read-only lock at a time.
        ///  Once acquired, you can use the lock's "Lock" version to obtain a writable lock.
        ///  Keep attempting until  sooner of following occurs:
        ///     1- time period specified by <paramref name="timeout"/> expires or
        ///     2- cancellation is requested via <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <param name="timeout">the max time to wait for</param>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> UpgradableRoLock(TimeSpan timeout, CancellationToken token) =>
            PerformUpgradableRoLock(timeout, token);

        /// <summary>
        ///  Obtain an upgradable read-only resource.  Only one thread may have an upgradable read-only lock at a time.
        ///  Once acquired, you can use the lock's "Lock" version to obtain a writable lock.
        /// Keep attempting until cancellation is requested via the <paramref name="token"/>
        /// parameter's  <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> UpgradableRoLock(CancellationToken token) =>
            PerformUpgradableRoLock(null, token);

        /// <summary>
        ///  Obtain an upgradable read-only resource.  Only one thread may have an upgradable read-only lock at a time.
        ///  Once acquired, you can use the lock's "Lock" version to obtain a writable lock.
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> UpgradableRoLock(TimeSpan timeout) => PerformUpgradableRoLock(timeout, CancellationToken.None);

        /// <summary>
        ///  Obtain an upgradable read-only resource.  Only one thread may have an upgradable read-only lock at a time.
        ///  Once acquired, you can use the lock's "Lock" version to obtain a writable lock.
        ///  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> UpgradableRoLock() => PerformUpgradableRoLock(DefaultTimeout, CancellationToken.None);

        /// <summary>
        ///  Obtain an upgradable read-only resource.  Only one thread may have an upgradable read-only lock at a time.
        ///  Once acquired, you can use the lock's "Lock" version to obtain a writable lock.
        /// This call can potentially block forever, unlike the
        /// other methods this vault exposes.  It may sometimes be desirable from a performance perspective
        /// not to check every so often for time expiration or cancellation requests.  For that reason, this explicitly
        /// named method exists.  Using this method, however, may cause a dead lock under certain circumstances (e.g.,
        /// you acquire this lock and another lock in different orders on different threads)
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> UpgradableRoLockBlockUntilAcquired()
            => PerformUpgradableRoLockBlockForever();
        #endregion

        #region Protected Methods
        /// <inheritdoc />
        protected override void ExecuteDispose(bool disposing, TimeSpan? timeout = null)
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
                            bool finishedDispose =
                                _disposeFlag.SignalDisposed();
                            Debug.Assert(finishedDispose);
                        }
                    }
                    catch (TimeoutException)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        throw;
                    }
                    catch (LockRecursionException e)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        Console.Error.WriteLine(e);
                        throw new RwLockAlreadyHeldThreadException(Thread.CurrentThread.ManagedThreadId, e);
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
        #endregion

        #region Private Methods
        private static BigValueList<TItem> CreateBigValueList([CanBeNull] VsEnumerableWrapper<TItem> items, int? capacity)
        {
            if (items == null && capacity == null)
                return new BigValueList<TItem>();
            if (items == null)
            {
                if (capacity < 0)
                    throw new ArgumentNegativeException<int>(nameof(capacity), capacity.Value);
                return new BigValueList<TItem>(capacity.Value);
            }
            var ret = new BigValueList<TItem>(items);
            if (capacity > ret.Capacity)
            {
                ret.Capacity = capacity.Value;
            }
            return ret;
        }

        private UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> PerformUpgradableRoLockBlockForever()
        {
            using (var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.UpgradableReadOnly))
            {
                (Box bx, Action<TimeSpan?, CancellationToken> acqAct, Action acqForever) = ilr.ReleaseUpgradable();
                Debug.Assert(bx != null && acqAct != null && acqForever != null);
                return UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem>.CreateUpgradableReadOnlyLockedResource(this, bx, acqAct, acqForever);
            }
        }

        private UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> PerformUpgradableRoLock(TimeSpan? timeout, CancellationToken token)
        {
            using (var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.UpgradableReadOnly))
            {
                (Box bx, Action<TimeSpan?, CancellationToken> acqAct, Action acqForever) = ilr.ReleaseUpgradable();
                Debug.Assert(bx != null && acqAct != null && acqForever != null);
                return UpgradableRoValListLockedResource<ReadWriteValueListVault<TItem>, TItem>.CreateUpgradableReadOnlyLockedResource(this, bx, acqAct, acqForever);
            }
        }

        private ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> PerformWriteLockBlockForever()
        {
            using (var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.ReadWrite))
            {
                return ValListLockedResource<ReadWriteValueListVault<TItem>, TItem>.CreateWritableLockedResource(this, ilr.Release());
            }
        }

        private ValListLockedResource<ReadWriteValueListVault<TItem>, TItem> PerformWriteLock(TimeSpan? timeout, CancellationToken token)
        {
            using (var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.ReadWrite))
            {
                return ValListLockedResource<ReadWriteValueListVault<TItem>, TItem>.CreateWritableLockedResource(this, ilr.Release());
            }
        }

        private RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> PerformRoLockBlockForever()
        {
            using (var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.ReadOnly))
            {
                return RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem>.CreateReadOnlyLockedResource(this, ilr.Release());
            }
        }

        private RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem> PerformRoLock(TimeSpan? timeout, CancellationToken token)
        {
            using (var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.ReadOnly))
            {
                return RoValListLockedResource<ReadWriteValueListVault<TItem>, TItem>.CreateReadOnlyLockedResource(this, ilr.Release());
            }
        } 
        #endregion
    }
}