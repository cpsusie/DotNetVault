using System;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomVaults;
using DotNetVault.LockedResources;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// Provides a basis for creating a custom mutable resource vault that returns a custom
    /// locked resource object, presumably to provide API convenience functions where possible
    /// </summary>
    /// <typeparam name="T">The protected type</typeparam>
    /// <remarks>See <see cref="StringBuilderVault"/> for how to use this class.</remarks>
    /// <remarks>This uses an MutableResourceVault that uses atomics as its base class.</remarks>
    public abstract class CustomizableAtomicMutableResourceVault<T> : MutableResourceVault<T>
    {

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="defaultTimeout">The default timeout</param>
        protected CustomizableAtomicMutableResourceVault(TimeSpan defaultTimeout) : base(defaultTimeout)
        {

        }

        /// <summary>
        /// Gets a locked vault mutable resource.  It is expected that this will be wrapped
        /// in a customized locked vault mutable resource to provide convenience functions.
        ///
        /// Once you get this object and UNTIL you release it to ultimate user via a
        /// Lock() or SpinLock() method annotated with the <see cref="UsingMandatoryAttribute"/>
        /// you are responsible for disposing the object (on the exceptional/early return cases) and returning
        /// it to the vault for use on other threads.
        /// </summary>
        /// <param name="timeout">how long to wait.  null means wait forever</param>
        /// <param name="token">a cancellation token that can be used to cancel the attempt
        /// to obtain the resource</param>
        /// <param name="spin">true for busy-wait, false for yielding wait</param>
        /// <returns>A <see cref="LockedVaultMutableResource{TVault,TResource}"/> for you to wrap
        /// in a customized LockedResourceVault object.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was not-null but was not positive</exception>
        /// <exception cref="TimeoutException">operation not completed in time (should be handled by user not by me or you ... except, see above, we need
        /// to dispose the resource ourselves if we've obtained it ... before rethrowing)</exception>
        /// <exception cref="OperationCanceledException">the operation was cancelled and the cancellation was received via <paramref name="token"/>.
        /// The user should handle this, not you or me (except, if we obtained the resource we need to dispose it before rethrowing).</exception>
        public LockedVaultMutableResource<MutableResourceVault<T>, T> GetLockedResourceBase(TimeSpan? timeout,
            CancellationToken token, bool spin) => PerformLock(timeout, token, spin);

    }
}