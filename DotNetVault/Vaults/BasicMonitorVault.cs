using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using DotNetVault.LockedResources;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// A vault that can protect VaultSafe types of resources.  Returns a more basic
    /// lock object that allows more direct interaction with the object than possible when
    /// protected resource is not vault safe.
    /// </summary>
    /// <typeparam name="T">the vault-safe protected resource type</typeparam>
    /// <remarks>This type uses Monitor and a sync object as its underlying synchronization mechanism</remarks>
    /// <remarks>The spin lock methods are provided in case you wish to switch back and forth between
    /// a <see cref="BasicVault{T}"/> and a <see cref="BasicMonitorVault{T}"/> and use spin-lock when
    /// configured with <see cref="BasicVault{T}"/>.  THIS VAULT DOES NOT SUPPORT SPINNING / BUSY WAITING.
    /// The SpinLock methods in THIS implementation are EXACTLY equivalent to the Lock methods.</remarks>
    public sealed class BasicMonitorVault<[VaultSafeTypeParam] T> : MonitorVault<T>, IBasicVault<T>
    {
        #region Static Properties
        /// <summary>
        /// Fallback timeout used when supplied timeout not valid.
        /// </summary>
        public static TimeSpan FallbackTimeout => TimeSpan.FromMilliseconds(250); 
        #endregion

        #region CTORS
        /// <inheritdoc />
        public BasicMonitorVault(TimeSpan defaultTimeout)
            : this(defaultTimeout, default) { }

        /// <summary>
        /// Creates a vault.  The value of <see cref="FallbackTimeout"/> will be used as default timeout.
        ///  </summary>
        /// <param name="initialValue">initial value of protected resource</param>
        public BasicMonitorVault(T initialValue) 
            : this(initialValue, FallbackTimeout) {}

        /// <summary>
        /// Creates a vault using the default value of the protected resource and the value
        /// specified by <see cref="FallbackTimeout"/> as the default wait period.
        /// </summary>
        public BasicMonitorVault() 
            : this(default, FallbackTimeout) { }

        /// <summary>
        /// CTOR -- vault with specified value and timeout
        /// </summary>
        /// <param name="initialValue">initial value of protected resource</param>
        /// <param name="defaultTimeout">default timeout period</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> was null</exception>
        public BasicMonitorVault(T initialValue, TimeSpan defaultTimeout) 
            : this(defaultTimeout, initialValue) {}

        private BasicMonitorVault(TimeSpan defaultTimeout, T initialValue) 
            : base(defaultTimeout)
        {
            Init(initialValue);
            Debug.Assert(BoxPtr != null);
        }
        #endregion

        #region Lock acquisition methods
        /// <summary>
        ///  Obtain the locked resource.  Keep attempting until
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
        public LockedMonVaultObject<BasicMonitorVault<T>, T> Lock(TimeSpan timeout, CancellationToken token) =>
            PerformLock(timeout, token);

        /// <summary>
        ///  Obtain the locked resource.  Yielding, (not busy), wait.  Keep attempting until
        ///  cancellation is requested via the <paramref name="token"/> parameter's
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> Lock(CancellationToken token) =>
            PerformLock(null, token);

        /// <summary>
        /// Obtain the locked resource.  Yielding, (not busy), wait.
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> Lock(TimeSpan timeout) => PerformLock(timeout, CancellationToken.None);

        /// <summary>
        /// Obtain the locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> Lock() => PerformLock(DefaultTimeout, CancellationToken.None);

        /// <summary>
        /// Obtain the locked resource.  This call can potentially block forever, unlike the
        /// other methods this vault exposes.  It may sometimes be desireable from a performance perspective
        /// not to check every so often for time expiration or cancellation requests.  For that reason, this explicitly
        /// named method exists.  Using this method, however, may cause a dead lock under certain circumstances (e.g.,
        /// you acquire this lock and another lock in different orders on different threads)
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> LockBlockUntilAcquired()
            => PerformLockBlockForever(); 
        #endregion

        #region Spin Methods to allow source code compatibility when switching types of vaults.
        /// <summary>
        /// Monitor vaults do not support spin locking.
        /// This call behaves EXACTLY the same as <see cref="Lock(System.TimeSpan,System.Threading.CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> SpinLock(TimeSpan timeout, CancellationToken token) =>
            PerformLock(timeout, token);
        /// <summary>
        /// Monitor vaults do not support spin locking.
        /// This call behaves EXACTLY the same as <see cref="Lock(System.Threading.CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> SpinLock(CancellationToken token) =>
            PerformLock(null,token);
        /// <summary>
        /// Monitor vaults do not support spin locking.
        /// This call behaves EXACTLY the same as <see cref="Lock(TimeSpan)"/>
        /// </summary>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> SpinLock(TimeSpan timeout) => PerformLock(timeout, CancellationToken.None);
        /// <summary>
        /// Monitor vaults do not support spin locking.
        /// This call behaves EXACTLY the same as <see cref="Lock()"/>
        /// </summary>
        [return: UsingMandatory]
        public LockedMonVaultObject<BasicMonitorVault<T>, T> SpinLock() => PerformLock(DisposeTimeout, CancellationToken.None);
        #endregion

        #region IBasicVault<T> Impl
        /// <summary>
        /// Wait for specified time period to obtain the resource, then copy protected value and return it
        /// </summary>
        /// <param name="timeout">the timeout</param>
        /// <returns>a copy of the protected value</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="LockAlreadyHeldThreadException"></exception>
        public T CopyCurrentValue(TimeSpan timeout)
        {
            T ret;
            using (var ilr = ExecuteGetInternalLockedResource(timeout, CancellationToken.None))
            {
                ret = ilr.Value;
            }
            return ret;
        }

        /// <summary>
        /// Attempt to get the locked resource for the time specified.  Return it with a flag indicating success/failure
        /// </summary>
        /// <param name="timeout">how long should we wait</param>
        /// <returns>A tuple with the value and a bool indicating success/failure.  If false, the value is undefined and probable equals
        /// default value of <typeparamref name="T"/></returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
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

        /// <summary>
        /// Try to obtain the resource, then overwrite it with the specified value
        /// </summary>
        /// <param name="timeout">how long should we wait</param>
        /// <param name="newValue">value to overwrite current protected value with</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
        /// <exception cref="TimeoutException">resource not obtained within time permitted by <paramref name="timeout"/>.</exception>
        public void SetCurrentValue(TimeSpan timeout, T newValue)
        {
            using var ilr = ExecuteGetInternalLockedResource(timeout, CancellationToken.None);
            ref T temp = ref ilr.Value;
            temp = newValue;
        }

        /// <summary>
        /// Try to obtain the resource.  Once obtained, overwrite it with specified value
        /// </summary>
        /// <param name="timeout">how long should we wait</param>
        /// <param name="newValue">the new value</param>
        /// <returns>true for success, false for failure</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
        public bool TrySetNewValue(TimeSpan timeout, T newValue)
        {
            try
            {
                using var ilr = ExecuteGetInternalLockedResource(timeout, CancellationToken.None);
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

        #region Private Methods
        private LockedMonVaultObject<BasicMonitorVault<T>, T> PerformLockBlockForever()
        {
            using (var ilr = ExecuteGetInternalLockedResourceBlockForever())
            {
                return LockedMonVaultObject<BasicMonitorVault<T>, T>.CreateLockedResource(this, ilr.Release());
            }
        }

        private LockedMonVaultObject<BasicMonitorVault<T>, T> PerformLock(TimeSpan? timeout, CancellationToken token)
        {
            using (var ilr = ExecuteGetInternalLockedResource(timeout, token))
            {
                return LockedMonVaultObject<BasicMonitorVault<T>, T>.CreateLockedResource(this, ilr.Release());
            }
        } 
        #endregion
    }
}
