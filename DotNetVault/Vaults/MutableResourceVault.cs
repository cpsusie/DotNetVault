using System;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomVaults;
using DotNetVault.Exceptions;
using DotNetVault.LockedResources;
using DotNetVault.Logging;
using JetBrains.Annotations;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// This vault is the out-of-the box vault designed to store resources that are not Vault-Safe.
    /// It is similar to <see cref="BasicVault{T}"/> in the functionality it exposes, save that 
    /// it does not provide convenience functions such as copying the value out of the vault
    /// and the locked resource it provides (of type <see cref="LockedVaultMutableResource{TVault,TResource}"/>
    /// restricts access to delegates annotated with the restrictive <see cref="NoNonVsCaptureAttribute"/> delgate
    /// to prevent leakage of the resource outside the LockedResource and/or vault.
    /// </summary>
    /// <typeparam name="T">The non-Vault-Safe resource the vault protects.</typeparam>
    public class MutableResourceVault<T> : Vault<T>
    {

        #region Factory Method

        /// <summary>
        /// Create a mutable resource vault to protect a mutable resource
        /// </summary>
        /// <param name="mutableResourceCreator">A delegate that will construct A NEW RESOURCE of type T.  It should NOT
        /// return an existing stringbuilder, it should simply be a delegate to the CTOR.  The vault can only isolate
        /// that which doesn't exist anywhere yet.</param>
        /// <param name="defaultTimeout">the default timeout</param>
        /// <returns>a an object of this type that stores the resource produced by <paramref name="mutableResourceCreator"/> and has a
        /// default timeout of <paramref name="defaultTimeout"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="mutableResourceCreator"/> was null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> was not positive.</exception>
        public static MutableResourceVault<T> CreateMutableResourceVault([NotNull] Func<T> mutableResourceCreator,
            TimeSpan defaultTimeout)
        {
            IMutableResourceVaultFactory<MutableResourceVault<T>, T> factory =
                MutableResourceVaultFactory<MutableResourceVault<T>>.CreateFactory();
            return factory.CreateMutableResourceVault(mutableResourceCreator, defaultTimeout,
                () => new MutableResourceVault<T>(defaultTimeout));
        }
        #endregion

        #region Protected CTOR
        /// <summary>
        /// Protected ctor used by factory methods or objects to create objects of this
        /// type or a derived type
        /// </summary>
        /// <param name="defaultTimeout">the default timeout</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/>
        /// was not positive.</exception>
        protected MutableResourceVault(TimeSpan defaultTimeout)
            : base(defaultTimeout) { }
        #endregion

        #region Public Resource Accessor Methods
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
        public LockedVaultMutableResource<MutableResourceVault<T>, T> Lock(TimeSpan timeout, CancellationToken token) => PerformLock(timeout, token, false);

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
        public LockedVaultMutableResource<MutableResourceVault<T>, T> Lock(CancellationToken token) =>
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
        public LockedVaultMutableResource<MutableResourceVault<T>, T> Lock(TimeSpan timeout) => PerformLock(timeout, CancellationToken.None, false);
        /// <summary>
        /// Obtain the locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultMutableResource<MutableResourceVault<T>, T> Lock() => PerformLock(DefaultTimeout, CancellationToken.None, false);
        /// <summary>
        /// Busy wait to get the resource 
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the locked resource object</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultMutableResource<MutableResourceVault<T>, T> SpinLock(TimeSpan timeout) => PerformLock(timeout, CancellationToken.None, true);
        /// <summary>
        /// Busy wait to get the resource for the time specified by <see cref="Vault{T}.DefaultTimeout"/>.
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        [return: UsingMandatory]
        public LockedVaultMutableResource<MutableResourceVault<T>, T> SpinLock() => PerformLock(DefaultTimeout, CancellationToken.None, true);
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
        public LockedVaultMutableResource<MutableResourceVault<T>, T> SpinLock(TimeSpan timeout, CancellationToken token) => PerformLock(timeout, token, false);
        
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
        public LockedVaultMutableResource<MutableResourceVault<T>, T> SpinLock(CancellationToken token) =>
            PerformLock(null, token, false);
        #endregion

        #region Protected Mutable Resource Vault Factory Nested Type Definition
        /// <summary>
        /// A factory for creating vaults of this type.  Can also be used to create derived vaults such as the <see cref="CustomizableMutableResourceVault{T}"/>
        /// or its example implementation .. see <see cref="StringBuilderVault"/>.
        /// </summary>
        /// <typeparam name="TDerivedMutableResourceVault">The type of the mutable resource vault this creates, must be of type <see cref="MutableResourceVault{T}"/>
        /// or a type derived from it.</typeparam>
        protected class MutableResourceVaultFactory<TDerivedMutableResourceVault> :
           IMutableResourceVaultFactory<TDerivedMutableResourceVault, T> where TDerivedMutableResourceVault : MutableResourceVault<T>
        {
            /// <summary>
            /// Creates a factory instance
            /// </summary>
            /// <returns>the factory instance</returns>
            internal static IMutableResourceVaultFactory<TDerivedMutableResourceVault, T> CreateFactory() => new MutableResourceVaultFactory<TDerivedMutableResourceVault>();

            /// <summary>
            /// Create a vault
            /// </summary>
            /// <param name="mutableResourceCreator">the function that creates a new mutable resource that doesn't get shared
            /// anywhere else, just returned</param>
            /// <param name="defaultTimeout">the default timeout</param>
            /// <param name="basicCtor">a delegate that executes <typeparamref name="TDerivedMutableResourceVault"/>'s constructor
            /// and returns it.</param>
            /// <returns>a vault of type <typeparamref name="TDerivedMutableResourceVault"/></returns>
            /// <exception cref="ArgumentNullException"><paramref name="mutableResourceCreator"/> or <paramref name="basicCtor"/> was null.
            /// </exception>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> not positve.</exception>
            public virtual TDerivedMutableResourceVault CreateMutableResourceVault(Func<T> mutableResourceCreator,
                TimeSpan defaultTimeout, Func<TDerivedMutableResourceVault> basicCtor)

            {
                if (mutableResourceCreator == null) throw new ArgumentNullException(nameof(mutableResourceCreator));
                if (basicCtor == null) throw new ArgumentNullException(nameof(basicCtor));

                TDerivedMutableResourceVault ret = null;

                T mutableResource;
                try
                {
                    mutableResource = mutableResourceCreator();
                    if (mutableResource == null)
                        throw new DelegateReturnedNullException<Func<T>>(mutableResourceCreator,
                            nameof(mutableResourceCreator));
                }
                catch (DelegateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DelegateThrewException<Func<T>>(mutableResourceCreator,
                        nameof(mutableResourceCreator), ex);
                }


                try
                {
                    ret = basicCtor();
                    ret.Init(mutableResource);
                }
                catch (Exception e)
                {
                    DebugLog.Log(e.ToString());
                    if (mutableResource is IDisposable d)
                    {
                        try
                        {
                            d.Dispose();
                        }
                        catch (Exception exception)
                        {
                            DebugLog.Log(exception);
                        }
                    }

                    try
                    {
                        ret?.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DebugLog.Log(exception);
                    }
                    throw;
                }
                return ret;
            }

            /// <summary>
            /// CTOR
            /// </summary>
            protected MutableResourceVaultFactory() { }

        } 
        #endregion

        #region Protected Methods
        /// <summary>
        /// Creates the LockedResource.  Until you actually return it to the ultimate consumer in one of the <see cref="Lock()"/> or <see cref="SpinLock()"/>
        /// methods (which are annotated with <see cref="UsingMandatoryAttribute"/>, guaranteeing disposal) it is YOUR responsiblity to make sure
        /// that the item is disposed IN ANY PATH THAT DOES NOT RESULT IN THE USER GETTING THE LOCKED RESOURCE REQUESTED.  This include exceptions,
        /// short-circuited returns, etc.
        /// </summary>
        /// <param name="timeout">How long should be wait.  If null, wait forever</param>
        /// <param name="token">A cancellation token that can be used to cancel attempting to obtain the resource.</param>
        /// <param name="spin">true for busy wait, false for yielding wait</param>
        /// <returns>A locked vault mutable resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was not-null but was not positive</exception>
        /// <exception cref="TimeoutException">operation not completed in time (should be handled by user not by me or you ... except, see above, we need
        /// to dispose the resource ourselves if we've obtained it ... before rethrowing)</exception>
        /// <exception cref="OperationCanceledException">the operation was cancelled and the cancellation was received via <paramref name="token"/>.
        /// The user should handle this, not you or me (except, if we obtained the resource we need to dispose it before rethrowing).</exception>
        protected virtual LockedVaultMutableResource<MutableResourceVault<T>, T> PerformLock(TimeSpan? timeout, CancellationToken token, bool spin)
        {
            if (timeout == null)
            {
                using (InternalLockedResource ilr = GetInternalLockedResource(token, spin))
                {
                    return LockedVaultMutableResource<MutableResourceVault<T>, T>.CreateLockedResource(this, ilr.Release());
                }
            }

            using (InternalLockedResource ilr = GetInternalLockedResource(timeout.Value, token, spin))
            {
                return LockedVaultMutableResource<MutableResourceVault<T>, T>.CreateLockedResource(this, ilr.Release());
            }
        } 
        #endregion
    }
}