using System;
using DotNetVault.Attributes;

namespace DotNetVault.Interfaces
{
    /// <summary>
    /// An interface specifying the properties and operations common to all vaults
    /// </summary>
    public interface IVault : IDisposable
    {
        /// <summary>
        /// True if a dispose is currently in progress but not yet complete
        /// </summary>
        bool DisposeInProgress { get; }
        /// <summary>
        /// True if the vault has been disposed.
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// Since inability to dispose lock may cause <see cref="IDisposable.Dispose()"/>
        /// to throw an exception (generally something to avoid), it is best to attempt
        /// disposal with this timed-out dispose method to detect problems without
        /// problematic exception being thrown on Dispose.
        /// </summary>
        /// <param name="timeout">How long to wait before stopping attempt to obtain resource for
        /// disposal purposes.</param>
        /// <returns>true for success, false for failure</returns>
        bool TryDispose(TimeSpan timeout);
        /// <summary>
        /// How long should we wait during the dispose method to attempt to acquire resource before throwing
        /// a <see cref="TimeoutException"/>.  Similar to <see cref="DefaultTimeout"/> but applies during <see cref="IDisposable.Dispose"/>
        /// method call only.
        /// </summary>
        TimeSpan DisposeTimeout { get; }
        /// <summary>
        /// How long should the sleep period be when trying to obtain lock.  IDeally as small as possible.
        /// 100 milliseconds at absolute most. 
        /// </summary>
        TimeSpan SleepInterval { get; }
        /// <summary>
        /// When not supplied with a timeout, how long Lock() and SpinLock()
        /// methods should wait before throwing a <see cref="TimeoutException"/>
        /// </summary>
        TimeSpan DefaultTimeout { get; }
    }

    /// <summary>
    /// A basic vault
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IBasicVault<[VaultSafeTypeParam] T> : IVault
    {
        /// <summary>
        /// Wait for specified time period to obtain the resource, then copy protected value and return it
        /// </summary>
        /// <param name="timeout">the timeout</param>
        /// <returns>a copy of the protected value</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        T CopyCurrentValue(TimeSpan timeout);
        /// <summary>
        /// Attempt to get the locked resource for the time specified.  Return it with a flag indicating success/failure
        /// </summary>
        /// <param name="timeout">how long should we wait</param>
        /// <returns>A tuple with the value and a bool indicating success/failure.  If false, the value is undefined and probable equals
        /// default value of <typeparamref name="T"/></returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
        (T value, bool success) TryCopyCurrentValue(TimeSpan timeout);
        /// <summary>
        /// Try to obtain the resource, then overwrite it with the specified value
        /// </summary>
        /// <param name="timeout">how long should we wait</param>
        /// <param name="newValue">value to overwrite current protected value with</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
        /// <exception cref="TimeoutException">resource not obtained within time permitted by <paramref name="timeout"/>.</exception>
        void SetCurrentValue(TimeSpan timeout, T newValue);
        /// <summary>
        /// Try to obtain the resource.  Once obtained, overwrite it with specified value
        /// </summary>
        /// <param name="timeout">how long should we wait</param>
        /// <param name="newValue">the new value</param>
        /// <returns>true for success, false for failure</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="ObjectDisposedException">object was disposed</exception>
        bool TrySetNewValue(TimeSpan timeout, T newValue);
    }
}
