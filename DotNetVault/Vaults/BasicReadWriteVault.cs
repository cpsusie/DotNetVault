using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using DotNetVault.LockedResources;
using TDisposeFlag = DotNetVault.DisposeFlag.TwoStepDisposeFlag;
namespace DotNetVault.Vaults
{
    /// <summary>
    /// This vault stores a vault safe type and allows you to obtain a number of different types of locks.
    /// Recursive obtaining of the lock is not allowed and will result in an exception of type <see cref="RwLockAlreadyHeldThreadException"/>
    /// Broadly, the locks are divided into two categories:
    ///     1- Read only locks (allow no mutation of the resource or any object in its graph) and 
    ///     2- Writable locks (allow mutation)
    /// A writable lock is EXCLUSIVE: when you hold it, you can be sure that you alone have access to the protected resource.
    /// A read-only lock is SHARED: any number of threads can simultaneously hold read-only locks (provided no thread holds a writable lock)
    /// but -- with the read-only lock -- mutation of the protected resource is prevented.  It may only be read.
    ///
    /// There are two kinds of read-only locks.
    ///     1- standard and
    ///     2- upgradable.
    /// There may be many simultaneous standard read-only locks, but there may be only one upgradable read-only lock at a time.
    /// (You can have, e.g. 0 writable locks, 1 upgradable read-only lock and (n where n >=0) standard read-only locks.
    /// An upgradable lock has mechanisms that allow you to use it to obtain a writable lock (without needing to release your upgradable read-only lock).
    ///
    /// The underlying synchronization mechanism is (currently) based on <see cref="ReaderWriterLockSlim"/> and you should review
    /// the documentation of that code to understand the fairness/prioritization strategies it used.
    /// <see href="https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim?view=netframework-4.8"/> 
    /// </summary>
    /// <typeparam name="T">A vault-safe type.</typeparam>
    /// <remarks>Lock here gets a writable lock because that is what lock does on other vaults.  SpinLock does not
    /// spin and is supplied for consistency with outer vaults only.  SpinLock overloads do exactly the same as their Lock counterparts.
    /// RoLock gets a readonly lock.  UpRoLock gets an upgradable readonly lock.  Since no other types of vault exist that provide
    /// readonly locks, no spin methods are included for these readonly varieties</remarks>
    public sealed class BasicReadWriteVault<[VaultSafeTypeParam] T> : ReadWriteVault<T>, IBasicVault<T>
    {
        #region Static Properties
        /// <summary>
        /// Fallback timeout used when supplied timeout not valid.
        /// </summary>
        public static TimeSpan FallbackTimeout => TimeSpan.FromMilliseconds(250);
        #endregion

        #region CTORS

        /// <inheritdoc />
        public BasicReadWriteVault(TimeSpan defaultTimeout) :
            this(default, defaultTimeout) =>
                Debug.Assert(CalledInit);


        /// <summary>
        /// Creates a vault.  The value of <see cref="FallbackTimeout"/> will be used as default timeout.
        ///  </summary>
        /// <param name="initialValue">initial value of protected resource</param>
        public BasicReadWriteVault(T initialValue) : this(initialValue, FallbackTimeout) => Debug.Assert(CalledInit);

        /// <summary>
        /// Creates a vault using the default value of the protected resource and the value
        /// specified by <see cref="FallbackTimeout"/> as the default wait period.
        /// </summary>
        public BasicReadWriteVault() : this(default, FallbackTimeout) => Debug.Assert(CalledInit);

        /// <summary>
        /// CTOR -- vault with specified value and timeout
        /// </summary>
        /// <param name="initialValue">initial value of protected resource</param>
        /// <param name="defaultTimeout">default timeout period</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> was null</exception>
        public BasicReadWriteVault(T initialValue, TimeSpan defaultTimeout) : base(defaultTimeout)
        {
            Init(initialValue);
            Debug.Assert(BoxPtr != null);
            Debug.Assert(CalledInit);
        }
        #endregion

        #region IBasicVault<T> Impl
        /// <inheritdoc />
        /// <exception cref="LockAlreadyHeldThreadException">This thread already holds a lock.</exception>
        public T CopyCurrentValue(TimeSpan timeout)
        {
            using var ilr =
                ExecuteGetInternalLockedResource(timeout, CancellationToken.None, AcquisitionMode.ReadOnly);
            return (ilr.Value);
        }

        /// <inheritdoc />
        public (T value, bool success) TryCopyCurrentValue(TimeSpan timeout)
        {
            T value;
            bool success;
            try
            {
                value = CopyCurrentValue(timeout);
                success = true;
            }
            catch (LockAlreadyHeldThreadException)
            {
                value = default;
                success = false;
            }
            catch (TimeoutException)
            {
                value = default;
                success = false;
            }
            return (value, success);
        }

        /// <inheritdoc />
        /// <exception cref="LockAlreadyHeldThreadException">This thread already holds a lock.</exception>
        public void SetCurrentValue(TimeSpan timeout, T newValue)
        {
            using var ilr =
                ExecuteGetInternalLockedResource(timeout, CancellationToken.None, AcquisitionMode.ReadWrite);
            ilr.Value = newValue;
        }

        /// <inheritdoc />
        public bool TrySetNewValue(TimeSpan timeout, T newValue)
        {
            try
            {
                using var ilr = ExecuteGetInternalLockedResource(timeout, CancellationToken.None, AcquisitionMode.ReadWrite);
                ref T temp = ref ilr.Value;
                temp = newValue;
                return true;
            }
            catch (LockAlreadyHeldThreadException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
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
        public ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T> RoLock(TimeSpan timeout, CancellationToken token) =>
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
        public ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T> RoLock(CancellationToken token) =>
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
        public ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T> RoLock(TimeSpan timeout) => PerformRoLock(timeout, CancellationToken.None);

        /// <summary>
        /// Obtain a read-only locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T> RoLock() => PerformRoLock(DefaultTimeout, CancellationToken.None);

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
        public ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T> RoLockBlockUntilAcquired()
            => PerformRoLockBlockForever();
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
        public ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T> UpgradableRoLock(TimeSpan timeout, CancellationToken token) =>
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
        public ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T> UpgradableRoLock(CancellationToken token) =>
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
        public ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T> UpgradableRoLock(TimeSpan timeout) => PerformUpgradableRoLock(timeout, CancellationToken.None);

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
        public ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T> UpgradableRoLock() => PerformUpgradableRoLock(DefaultTimeout, CancellationToken.None);

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
        public ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T> UpgradableRoLockBlockUntilAcquired()
            => PerformUpgradableRoLockBlockForever();
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
        public RwLockedResource<BasicReadWriteVault<T>, T> Lock(TimeSpan timeout, CancellationToken token) =>
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
        public RwLockedResource<BasicReadWriteVault<T>, T> Lock(CancellationToken token) =>
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
        public RwLockedResource<BasicReadWriteVault<T>, T> Lock(TimeSpan timeout) => PerformWriteLock(timeout, CancellationToken.None);

        /// <summary>
        /// Obtain a writable locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public RwLockedResource<BasicReadWriteVault<T>, T> Lock() => PerformWriteLock(DefaultTimeout, CancellationToken.None);

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
        public RwLockedResource<BasicReadWriteVault<T>, T> LockBlockUntilAcquired()
            => PerformWriteLockBlockForever();
        #endregion

        #region Spin Methods to allow source code compatibility when switching types of vaults.
        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(TimeSpan,CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public RwLockedResource<BasicReadWriteVault<T>, T> SpinLock(TimeSpan timeout, CancellationToken token) =>
            PerformWriteLock(timeout, token);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public RwLockedResource<BasicReadWriteVault<T>, T> SpinLock(CancellationToken token) =>
            PerformWriteLock(null, token);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(TimeSpan)"/>
        /// </summary>
        [return: UsingMandatory]
        public RwLockedResource<BasicReadWriteVault<T>, T> SpinLock(TimeSpan timeout) => PerformWriteLock(timeout, CancellationToken.None);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock()"/>
        /// </summary>
        [return: UsingMandatory]
        public RwLockedResource<BasicReadWriteVault<T>, T> SpinLock() => PerformWriteLock(DefaultTimeout, CancellationToken.None);
        #endregion

        #region protected methods
        /// <summary>
        /// Obtain a write lock then destroy the locked resource, finally, destroy
        /// the locker object.
        /// </summary>
        /// <param name="disposing">true if called by user code, false if called by GC
        /// during finalize</param>
        /// <param name="timeout">The timeout to use if any</param>
        /// <exception cref="RwLockAlreadyHeldThreadException">The calling thread already holds the lock</exception>
        /// <exception cref="TimeoutException">Calling thread could not obtain writable lock in period specified by
        /// <paramref name="timeout"/></exception>
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
        private RwLockedResource<BasicReadWriteVault<T>, T> PerformWriteLockBlockForever()
        {
            using (var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.ReadWrite))
            {
                return RwLockedResource<BasicReadWriteVault<T>, T>.CreateWritableLockedResource(this, ilr.Release());
            }
        }

        private RwLockedResource<BasicReadWriteVault<T>, T> PerformWriteLock(TimeSpan? timeout, CancellationToken token)
        {
            using (var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.ReadWrite))
            {
                return RwLockedResource<BasicReadWriteVault<T>, T>.CreateWritableLockedResource(this, ilr.Release());
            }
        }

        private ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T> PerformUpgradableRoLockBlockForever()
        {
            using (var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.UpgradableReadOnly))
            {
                (Box bx, Action<TimeSpan?, CancellationToken> acqAct, Action acqForever)= ilr.ReleaseUpgradable();
                Debug.Assert(bx != null && acqAct != null && acqForever != null);
                return ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T>.CreateUpgradableReadOnlyLockedResource(this, bx, acqAct, acqForever);
            }
        }

        private ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T> PerformUpgradableRoLock(TimeSpan? timeout, CancellationToken token)
        {
            using (var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.UpgradableReadOnly))
            {
                (Box bx, Action<TimeSpan?, CancellationToken> acqAct, Action acqForever) = ilr.ReleaseUpgradable();
                Debug.Assert(bx != null && acqAct != null && acqForever != null);
                return ReadOnlyUpgradableRwLockedResource<BasicReadWriteVault<T>, T>.CreateUpgradableReadOnlyLockedResource(this, bx, acqAct, acqForever);
            }
        }

        private ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T> PerformRoLockBlockForever()
        {
            using (var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.ReadOnly))
            {
                return ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T>.CreateReadOnlyLockedResource( this, ilr.Release());
            }
        }

        private ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T> PerformRoLock(TimeSpan? timeout, CancellationToken token)
        {
            using (var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.ReadOnly))
            {
                return ReadOnlyRwLockedResource<BasicReadWriteVault<T>, T>.CreateReadOnlyLockedResource(this, ilr.Release());
            }
        }
        #endregion

    }
}
