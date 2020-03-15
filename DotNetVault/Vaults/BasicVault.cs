using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using DotNetVault.LockedResources;
using JetBrains.Annotations;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// A vault that can protect VaultSafe types of resources.  Returns a more basic
    /// lock object that allows more direct interaction with the object than possible when
    /// protected resource is not vault safe.
    /// </summary>
    /// <typeparam name="T">the vault-safe protected resource type</typeparam>
    /// <remarks>This type uses atomics as its underlying synchronization mechanism</remarks>
    public sealed class BasicVault<[VaultSafeTypeParam] T> : AtomicVault<T>, IBasicVault<T>
    {
        /// <summary>
        /// Fallback timeout used when supplied timeout not valid.
        /// </summary>
        public static TimeSpan FallbackTimeout => TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Create a basic vault that uses atomics as its synchronization
        /// mechanism using the specified initial value and the specified default timeout.
        /// </summary>
        /// <param name="initialValue">the initial value</param>
        /// <param name="defaultTimeout">default timeout to use when not otherwise
        /// specified</param>
        /// <returns>A BasicVault that uses atomics.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="defaultTimeout"/>
        /// was not positive.</exception>
        [NotNull] public static BasicVault<T> CreateAtomicBasicVault(T initialValue, TimeSpan defaultTimeout)
            => new BasicVault<T>(initialValue, defaultTimeout);
        /// <summary>
        /// Create an atomic basic vault with <see cref="FallbackTimeout"/> as its default
        /// timeout and <paramref name="initialValue"/> as its initial value.
        /// </summary>
        /// <param name="initialValue">the initial value</param>
        /// <returns>A BasicVault that uses atomics.</returns>
        [NotNull] public static BasicVault<T> CreateAtomicBasicVault(T initialValue)
            => new BasicVault<T>(initialValue);

        /// <summary>
        /// Create a basic vault that uses atomics as its synchronization
        /// mechanism using the specified default timeout.
        /// </summary>
        /// <param name="defaultTimeout">the default timeout</param>
        /// <returns>A basic vault that uses atomics</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/>
        /// was not positive</exception>
        [NotNull] public static BasicVault<T> CreateAtomicBasicVault(TimeSpan defaultTimeout)
            => new BasicVault<T>(defaultTimeout);

        /// <summary>
        /// Create a basic vault that uses atomics as its synchronization mechanism.  Will
        /// use <see cref="FallbackTimeout"/> as its default timeout.
        /// </summary>
        /// <returns>A basic vault that uses atomics.</returns>
        [NotNull] public static BasicVault<T> CreateAtomicBasicVault() => new BasicVault<T>();

        #region CTORS

        /// <inheritdoc />
        public BasicVault(TimeSpan defaultTimeout) 
            : base(defaultTimeout) {}

        /// <summary>
        /// Creates a vault.  The value of <see cref="FallbackTimeout"/> will be used as default timeout.
        ///  </summary>
        /// <param name="initialValue">initial value of protected resource</param>
        public BasicVault(T initialValue) : this(initialValue, FallbackTimeout)
        {

        }

        /// <summary>
        /// Creates a vault using the default value of the protected resource and the value
        /// specified by <see cref="FallbackTimeout"/> as the default wait period.
        /// </summary>
        public BasicVault() : this(default, FallbackTimeout)
        {

        }

        /// <summary>
        /// CTOR -- vault with specified value and timeout
        /// </summary>
        /// <param name="initialValue">initial value of protected resource</param>
        /// <param name="defaultTimeout">default timeout period</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> was null</exception>
        public BasicVault(T initialValue, TimeSpan defaultTimeout) : base(defaultTimeout)
        {
            Init(initialValue);
            Debug.Assert(BoxPtr != null);
        }
        #endregion

        /// <summary>
        ///  Obtain the locked resource.  Yielding, (not busy), wait.  Keep attempting until
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
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> Lock(TimeSpan timeout, CancellationToken token) =>
            PerformLock(timeout, token, false);

        /// <summary>
        ///  Obtain the locked resource.  Yielding, (not busy), wait.  Keep attempting until
        ///  cancellation is requested via the <paramref name="token"/> parameter's
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> Lock(CancellationToken token) =>
            PerformLock(null, token, false);

        /// <summary>
        /// Obtain the locked resource.  Yielding, (not busy), wait.
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> Lock(TimeSpan timeout) => PerformLock(timeout, CancellationToken.None, false);

        /// <summary>
        /// Obtain the locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> Lock() => PerformLock(DefaultTimeout, CancellationToken.None, false);

        /// <summary>
        ///  Obtain the locked resource.  Busy wait.  Keep attempting until
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
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> SpinLock(TimeSpan timeout, CancellationToken token) =>
            PerformLock(timeout, token, true);

        /// <summary>
        ///  Obtain the locked resource.  Busy wait.  Keep attempting until
        ///  cancellation is requested via the <paramref name="token"/> parameter's
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> SpinLock(CancellationToken token) =>
            PerformLock(null, token, true);

        /// <summary>
        /// Busy wait to get the resource 
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the locked resource object</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> SpinLock(TimeSpan timeout) => PerformLock(timeout, CancellationToken.None, true);
        /// <summary>
        /// Busy wait to get the resource for the time specified by <see cref="Vault{T}.DefaultTimeout"/>.
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultObject<BasicVault<T>, T> SpinLock() => PerformLock(DefaultTimeout, CancellationToken.None, true);

        /// <summary>
        /// Wait for specified time period to obtain the resource, then copy protected value and return it
        /// </summary>
        /// <param name="timeout">the timeout</param>
        /// <returns>a copy of the protected value</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        public T CopyCurrentValue(TimeSpan timeout)
        {
            T ret;
            using (var ilr = GetInternalLockedResource(timeout))
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
            using var ilr = GetInternalLockedResource(timeout);
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
                using var ilr = GetInternalLockedResource(timeout);
                ref T temp = ref ilr.Value;
                temp = newValue;
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }


        private LockedVaultObject<BasicVault<T>, T> PerformLock(TimeSpan? timeout, CancellationToken token, bool spin)
        {
            if (timeout == null)
            {
                using (var ilr = GetInternalLockedResource(token, spin))
                {
                    return LockedVaultObject<BasicVault<T>, T>.CreateLockedResource(this, ilr.Release());
                }
            }

            using (var ilr = GetInternalLockedResource(timeout.Value, token, spin))
            {
                return LockedVaultObject<BasicVault<T>, T>.CreateLockedResource(this, ilr.Release());
            }
        }
    }
 
}