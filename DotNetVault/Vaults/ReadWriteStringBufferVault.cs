using System;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomLockedResources;
using DotNetVault.Interfaces;
using JetBrains.Annotations;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// A customized read-write vault that holds a string builder.
    ///
    /// The locked resource objects provide -- to the extent possible --
    /// the methods exposed by a string builder, with certain additions and --
    /// especially for the read-only version -- subtractions.
    /// </summary>
    public sealed partial class ReadWriteStringBufferVault : IBasicVault<string>
    {
        #region Properties
        /// <inheritdoc />
        public bool DisposeInProgress => _wrappedVault.DisposeInProgress;
        /// <inheritdoc />
        public bool IsDisposed => _wrappedVault.IsDisposed;
        /// <inheritdoc />
        public TimeSpan DisposeTimeout => _wrappedVault.DisposeTimeout;
        /// <inheritdoc />
        public TimeSpan SleepInterval => _wrappedVault.SleepInterval;
        /// <inheritdoc />
        public TimeSpan DefaultTimeout => _wrappedVault.DefaultTimeout; 
        #endregion

        #region CTORS
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="timeout">Default timeout</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was not positive.</exception>
        public ReadWriteStringBufferVault(TimeSpan timeout)
            : this(timeout, () => new StringBuilder()) { }

        /// <summary>                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               
        /// CTOR
        /// </summary>
        /// <param name="timeout">Default timeout</param>
        /// <param name="sbCtor">method to create initial string builder.  Should
        /// create a new StringBuilder only accessible in return value, do not provide
        /// reference to existing StringBuilder or save the StringBuilder contents ...
        /// create and return only.</param>
        /// <exception cref="ArgumentNullException"><paramref name="sbCtor"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was not positive.</exception>
        public ReadWriteStringBufferVault(TimeSpan timeout, [NotNull] Func<StringBuilder> sbCtor) =>
            _wrappedVault = new StringBuilderCustomVault(
                timeout > TimeSpan.Zero
                    ? timeout
                    : throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Parameter must be positive."),
                sbCtor ?? throw new ArgumentNullException(nameof(sbCtor))); 
        #endregion

        #region Dispose Methods
        /// <inheritdoc />
        public void Dispose() => Dispose(true);
        /// <inheritdoc />
        public bool TryDispose(TimeSpan timeout) => _wrappedVault.TryDispose(timeout); 
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
        public StringBuilderRoLockedResource RoLock(TimeSpan timeout, CancellationToken token) =>
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
        public StringBuilderRoLockedResource RoLock(CancellationToken token) =>
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
        public StringBuilderRoLockedResource RoLock(TimeSpan timeout) => PerformRoLock(timeout, CancellationToken.None);

        /// <summary>
        /// Obtain a read-only locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public StringBuilderRoLockedResource RoLock() => PerformRoLock(DefaultTimeout, CancellationToken.None);

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
        public StringBuilderRoLockedResource RoLockBlockUntilAcquired()
        {
            var temp = _wrappedVault.PerformRoLockBlockForever();
            return StringBuilderRoLockedResource.CreatedStringBuilderRoLockedResource(in temp);
        }
        #endregion

        #region upgradable readonly lock acquisition methods
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
        public StringBuilderUpgradableRoLockedResource UpgradableRoLock(TimeSpan timeout, CancellationToken token) =>
            PerformUpgradableRoLock(timeout, token);
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
        public StringBuilderUpgradableRoLockedResource UpgradableRoLock(CancellationToken token) =>
            PerformUpgradableRoLock(null, token);
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
        public StringBuilderUpgradableRoLockedResource UpgradableRoLock(TimeSpan timeout) => PerformUpgradableRoLock(timeout, CancellationToken.None);
        /// <summary>
        /// Obtain a read-only locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public StringBuilderUpgradableRoLockedResource UpgradableRoLock() => PerformUpgradableRoLock(DefaultTimeout, CancellationToken.None);
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
        public StringBuilderUpgradableRoLockedResource UpgradableRoLockBlockUntilAcquired()
        {
            var temp = _wrappedVault.PerformUpgradableRoLockBlockForever();
            return StringBuilderUpgradableRoLockedResource.CreatedStringBuilderUpgradableRoLockedResource(in temp);
        }
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
        public StringBuilderRwLockedResource Lock(TimeSpan timeout, CancellationToken token) =>
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
        public StringBuilderRwLockedResource Lock(CancellationToken token) =>
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
        public StringBuilderRwLockedResource Lock(TimeSpan timeout) => PerformWriteLock(timeout, CancellationToken.None);

        /// <summary>
        /// Obtain a writable locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public StringBuilderRwLockedResource Lock() => PerformWriteLock(DefaultTimeout, CancellationToken.None);

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
        public StringBuilderRwLockedResource LockBlockUntilAcquired()
        {
            var temp = _wrappedVault.PerformRwLockBlockForever();
            return StringBuilderRwLockedResource.CreatedStringBuilderRwLockedResource(in temp);
        }
        #endregion

        #region Spin Methods to allow source code compatibility when switching types of vaults.
        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(TimeSpan,CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public StringBuilderRwLockedResource SpinLock(TimeSpan timeout, CancellationToken token) =>
            PerformWriteLock(timeout, token);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public StringBuilderRwLockedResource SpinLock(CancellationToken token) =>
            PerformWriteLock(null, token);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock(TimeSpan)"/>
        /// </summary>
        [return: UsingMandatory]
        public StringBuilderRwLockedResource SpinLock(TimeSpan timeout) => PerformWriteLock(timeout, CancellationToken.None);

        /// <summary>
        ///  Read Write Vaults do not support spinning.  Exactly the same as <see cref="Lock()"/>
        /// </summary>
        [return: UsingMandatory]
        public StringBuilderRwLockedResource SpinLock() => PerformWriteLock(DefaultTimeout, CancellationToken.None);
        #endregion

        #region IBasicVault<string>
        /// <inheritdoc />
        public string CopyCurrentValue(TimeSpan timeout)
        {
            using var lck = RoLock(timeout);
            return lck.ToString();
        }

        /// <inheritdoc />
        public (string value, bool success) TryCopyCurrentValue(TimeSpan timeout)
        {
            bool ok;
            string val;
            try
            {
                using var lck = RoLock(timeout);
                val = lck.ToString();
                ok = val != null;
            }
            catch (TimeoutException)
            {
                val = null;
                ok = false;
            }

            return (val, ok);
        }

        /// <inheritdoc />
        public void SetCurrentValue(TimeSpan timeout, string newValue)
        {
            using var lck = Lock(timeout);
            lck.Clear();
            lck.Append(newValue);
        }

        /// <inheritdoc />
        public bool TrySetNewValue(TimeSpan timeout, string newValue)
        {
            bool ok;
            try
            {
                using var lck = Lock(timeout);
                lck.Clear();
                lck.Append(newValue);
                ok = true;
            }
            catch (TimeoutException)
            {
                ok = false;
            }
            return ok;
        }
        #endregion

        #region Private Methods
        private StringBuilderRoLockedResource PerformRoLock(TimeSpan? timeout, CancellationToken token)
        {
            var temp = _wrappedVault.PerformRoLock(timeout, token);
            return StringBuilderRoLockedResource.CreatedStringBuilderRoLockedResource(in temp);
        }

        private StringBuilderRwLockedResource PerformWriteLock(TimeSpan? timeout, CancellationToken token)
        {
            var temp = _wrappedVault.PerformRwLock(timeout, token);
            return StringBuilderRwLockedResource.CreatedStringBuilderRwLockedResource(in temp);
        }

        private StringBuilderUpgradableRoLockedResource PerformUpgradableRoLock(TimeSpan? timeout,
            CancellationToken token)
        {
            var temp = _wrappedVault.PerformUpgradableRoLock(timeout, token);
            return StringBuilderUpgradableRoLockedResource.CreatedStringBuilderUpgradableRoLockedResource(in temp);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _wrappedVault.Dispose();
            }
        } 
        #endregion

        #region Privates
        [NotNull] private readonly StringBuilderCustomVault _wrappedVault; 
        #endregion
    }

    partial class ReadWriteStringBufferVault
    {
        #region Nested Type Def
        internal sealed class StringBuilderCustomVault : CustomReadWriteVaultBase<StringBuilder>
        {
            public StringBuilderCustomVault(TimeSpan defaultTimeout) : this(defaultTimeout, () => new StringBuilder())
            {
            }

            public StringBuilderCustomVault(TimeSpan defaultTimeout, [NotNull] Func<StringBuilder> func) : base(
                defaultTimeout, func)
            {
            }


            internal InternalCustomRwLockedResource<StringBuilderCustomVault> PerformRwLock(TimeSpan? timeout,
                CancellationToken token)
            {
                var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.ReadWrite);
                try
                {
                    return InternalCustomRwLockedResource<StringBuilderCustomVault>.CreateWritableLockedResource(
                        ref ilr, this);
                }
                finally
                {
                    ilr.Dispose();
                }
            }

            internal InternalCustomRwLockedResource<StringBuilderCustomVault> PerformRwLockBlockForever()
            {
                var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.ReadWrite);
                try
                {
                    return InternalCustomRwLockedResource<StringBuilderCustomVault>.CreateWritableLockedResource(
                        ref ilr, this);
                }
                finally
                {
                    ilr.Dispose();
                }
            }

            internal InternalCustomRoLockedResource<StringBuilderCustomVault> PerformRoLock(TimeSpan? timeout,
                CancellationToken token)
            {
                var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.ReadOnly);
                try
                {
                    return InternalCustomRoLockedResource<StringBuilderCustomVault>.CreateRoLockedResource(ref ilr,
                        this);
                }
                finally
                {
                    ilr.Dispose();
                }
            }

            internal InternalCustomRoLockedResource<StringBuilderCustomVault> PerformRoLockBlockForever()
            {
                var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.ReadOnly);
                try
                {
                    return InternalCustomRoLockedResource<StringBuilderCustomVault>.CreateRoLockedResource(ref ilr,
                        this);
                }
                finally
                {
                    ilr.Dispose();
                }
            }

            internal InternalCustomUpgradableRoLockedResource<StringBuilderCustomVault> PerformUpgradableRoLock(TimeSpan? timeout,
                CancellationToken token)
            {
                var ilr = ExecuteGetInternalLockedResource(timeout, token, AcquisitionMode.UpgradableReadOnly);
                try
                {
                    var upgradeAction = ilr.UpgradeAction;
                    var upgradeForeverAction = ilr.UpgradePotentialWaitForeverAction;
                    return InternalCustomUpgradableRoLockedResource<StringBuilderCustomVault>
                        .CreateUpgradableRoLockedResource(this, ilr.Release(), upgradeAction ?? throw new InvalidOperationException("The lock is not upgradable."),
                            upgradeForeverAction ?? throw new InvalidOperationException("The lock is not upgradable."));
                }
                finally
                {
                    ilr.Dispose();
                }
            }

            internal InternalCustomUpgradableRoLockedResource<StringBuilderCustomVault> PerformUpgradableRoLockBlockForever()
            {
                var ilr = ExecuteGetInternalLockedResourceBlockForever(AcquisitionMode.UpgradableReadOnly);
                try
                {
                    var upgradeAction = ilr.UpgradeAction;
                    var upgradeForeverAction = ilr.UpgradePotentialWaitForeverAction;
                    return InternalCustomUpgradableRoLockedResource<StringBuilderCustomVault>
                        .CreateUpgradableRoLockedResource(this, ilr.Release(), upgradeAction ?? throw new InvalidOperationException("The lock is not upgradable."),
                            upgradeForeverAction ?? throw new InvalidOperationException("The lock is not upgradable."));
                }
                finally
                {
                    ilr.Dispose();
                }
            }

        } 
        #endregion
    }
}
