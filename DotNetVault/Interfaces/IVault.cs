using System;

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
}
