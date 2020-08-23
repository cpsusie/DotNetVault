using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.LockedResources
{
    /// <summary>
    /// This locked resource type is returned by <see cref="ReadWriteVault{T}"/>s whose generic parameter is vault-safe
    /// when an upgradable read-only lock is requested. It contains Lock methods that can be used to obtain a writable locked resource
    /// by "upgrading".
    ///
    /// You can read the value by reference and access non mutating methods via the <see cref="Value"/> property.
    /// May not be used to propagate changes.  Ref local aliasing is forbidden by the <see cref="BasicVaultProtectedResourceAttribute"/>
    /// </summary>
    /// <typeparam name="TVault">The vault type</typeparam>
    /// <typeparam name="T">The resource type (must be vault safe) </typeparam>
    [NoCopy]
    [RefStruct]
    public readonly ref struct ReadOnlyUpgradableRwLockedResource<TVault, [VaultSafeTypeParam] T> where TVault : ReadWriteVault<T>
    {
        internal static ReadOnlyUpgradableRwLockedResource<TVault, T> CreateUpgradableReadOnlyLockedResource([NotNull] TVault v,
            [NotNull] Vault<T>.Box b, [NotNull] Action<TimeSpan?, CancellationToken> upgradeAction, [NotNull] Action upgradeForeverAction)
        {
            Debug.Assert(v != null && b != null && upgradeAction != null && upgradeForeverAction != null);
            Func<ReadWriteVault<T>, Vault<T>.Box, AcquisitionMode, Vault<T>.Box> releaseMethod =
                ReadWriteVault<T>.ReleaseResourceMethod;
            return new ReadOnlyUpgradableRwLockedResource<TVault, T>(v, b, releaseMethod, upgradeAction,
                upgradeForeverAction);
        }

        /// <summary>
        /// Access to the protected value by readonly reference
        /// </summary>
        [BasicVaultProtectedResource(true)]
        public ref readonly T Value => ref _box.RoValue;

        /// <summary>
        /// The underlying vault's default timeout
        /// </summary>
        public TimeSpan DefaultTimeout => _vault?.DefaultTimeout ?? TimeSpan.Zero;

        private ReadOnlyUpgradableRwLockedResource([NotNull] TVault v, [NotNull] Vault<T>.Box b,
            [NotNull] Func<TVault, Vault<T>.Box, AcquisitionMode, Vault<T>.Box> disposeMethod, [NotNull] Action<TimeSpan?, CancellationToken> upgradeAction,
            [NotNull] Action upgradeForeverAction)
        {
            Debug.Assert(b != null && v != null && disposeMethod != null && upgradeAction != null && upgradeForeverAction != null);
            _box = b;
            _disposeMethod = disposeMethod;
            _flag = new DisposeFlag();
            _vault = v;
            _upgradeForever = upgradeForeverAction;
            _upgradeWithWait = upgradeAction;
        }

        /// <summary>
        /// release the lock and return the protected resource to vault for use by others
        /// </summary>
        [NoDirectInvoke]
        public void Dispose()
        {
            if (_flag?.TrySet() == true)
            {
                Vault<T>.Box b = _box;
                // ReSharper disable once RedundantAssignment DEBUG vs RELEASE
                var temp = _disposeMethod(_vault, b, Mode);
                Debug.Assert(temp == null);
            }
        }

        /// <summary>
        ///  Obtain a writable locked resource.  Keep attempting until
        ///  sooner of following occurs:
        ///     1- time period specified by <paramref name="timeout"/> expires or
        ///     2- cancellation is requested via <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <param name="timeout">the max time to wait for</param>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
        /// <exception cref="ArgumentOutOfRangeException">Non-positive <paramref name="timeout"/></exception>
        /// <exception cref="TimeoutException">Could not obtain write lock in time specified by <paramref name="timeout"/></exception>
        /// <exception cref="OperationCanceledException">A cancellation request was propagated to the <paramref name="token"/></exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public RwLockedResource<TVault, T> Lock(TimeSpan timeout, CancellationToken token)
        {
            if (_box == null) throw new InvalidOperationException("This object is invalid.");
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"");
            return UpgradeAction(timeout, token);
        }

        /// <summary>
        /// Get a writable lock.
        /// </summary>
        /// <param name="timeout">how long to keep attempting before throwing <see cref="TimeoutException"/></param>
        /// <returns>the resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
        /// <exception cref="ArgumentOutOfRangeException">Non-positive <paramref name="timeout"/></exception>
        /// <exception cref="TimeoutException">Could not obtain write lock in time specified by <paramref name="timeout"/></exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public RwLockedResource<TVault, T> Lock(TimeSpan timeout)
        {
            if (_box == null) throw new InvalidOperationException("This object is invalid.");
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"");
            return UpgradeAction(timeout, CancellationToken.None);
        }


        /// <summary>
        ///  Obtain a writable locked resource. 
        /// </summary>
        /// <param name="token">a token to which cancellation requests can be propagated.</param>
        /// <returns>the resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
        /// <exception cref="OperationCanceledException">A cancellation request was propagated to the <paramref name="token"/></exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public RwLockedResource<TVault, T> Lock(CancellationToken token)
        {
            if (_box == null) throw new InvalidOperationException("This object is invalid.");
            return UpgradeAction(null, token);
        }

        /// <summary>
        /// Obtain the writable locked resource
        /// </summary>
        /// <returns>the locked resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized
        /// validly</exception>
        /// <exception cref="TimeoutException">Unable to obtain locked resource in the underlying vault's
        /// <see cref="DefaultTimeout"/>.</exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public RwLockedResource<TVault, T> Lock()
        {
            if (_box == null) throw new InvalidOperationException("This object is invalid.");
            return UpgradeAction(_vault.DefaultTimeout, CancellationToken.None);
        }

        /// <summary>
        /// Wait to obtain the WriteLock potentially forever
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been </exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock.</exception>
        [return: UsingMandatory]
        public RwLockedResource<TVault, T> LockWaitForever()
        {
            if (_box == null) throw new InvalidOperationException("This object is invalid.");
            return UpgradeWaitForever();
        }

        private RwLockedResource<TVault, T> UpgradeAction(TimeSpan? ts, CancellationToken token)
        {
            if (_box == null)  throw new InvalidOperationException("This object is invalid.");
            if (ts.HasValue && ts <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ts), ts, @"Not null timespan must have positive value.");
            _upgradeWithWait(ts, token);
            return RwLockedResource<TVault, T>.CreateWritableLockedResource(_vault, _box);
        }

        private RwLockedResource<TVault, T> UpgradeWaitForever()
        {
            if (_box == null) throw new InvalidOperationException("This object is invalid.");
            _upgradeForever();
            return RwLockedResource<TVault, T>.CreateWritableLockedResource(_vault, _box);
        }

        private readonly DisposeFlag _flag;
        private readonly Action<TimeSpan?, CancellationToken> _upgradeWithWait;
        private readonly Action _upgradeForever;
        private readonly Func<TVault, Vault<T>.Box, AcquisitionMode, Vault<T>.Box> _disposeMethod;
        private readonly Vault<T>.Box _box;
        private readonly TVault _vault;
        private const AcquisitionMode Mode = AcquisitionMode.UpgradableReadOnly;
    }
}
