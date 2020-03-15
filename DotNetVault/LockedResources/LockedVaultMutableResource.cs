using System;
using System.Diagnostics;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomVaults;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.LockedResources
{
    #region Locked Mutable Resource Delegates
    // ReSharper disable TypeParameterCanBeVariant
    /// <summary>
    /// A delegate used to perform a query on the current status of an object locked by a
    /// <see cref="LockedVaultMutableResource{TVault,TResource}"/> object or by a custom version
    /// thereof <seealso cref="StringBuilderVault"/>.
    /// </summary>
    /// <typeparam name="TResource">The type of protected resource</typeparam>
    /// <typeparam name="TResult">The return type of the query (must be vault-safe)</typeparam>
    /// <param name="res">the protected resource </param>
    /// <returns>The result</returns>
    /// <remarks>See <see cref="NoNonVsCaptureAttribute"/> and the limitations it imposes on the semantics of
    /// delegates so-annotated.</remarks>
    [NoNonVsCapture]
    public delegate TResult VaultQuery<TResource, [VaultSafeTypeParam] TResult>(in TResource res);
    /// <summary>
    /// A delegate used to perform a query on the current status of an object locked by a
    /// <see cref="LockedVaultMutableResource{TVault,TResource}"/> object or by a custom version
    /// thereof <seealso cref="StringBuilderVault"/>.
    /// </summary>
    /// <typeparam name="TResource">The type of protected resource</typeparam>
    /// <typeparam name="TResult">The return type of the query (must be vault-safe) </typeparam>
    /// <typeparam name="TAncillary">An ancillary parameter, which must be a vault-safe value passed by
    /// constant reference, used in the delegate.</typeparam>
    /// <param name="res">the protected resource </param>
    /// <param name="ancillary">an ancillary vault-safe value passed by readonly-reference</param>
    /// <returns>The result</returns>
    /// <remarks>See <see cref="NoNonVsCaptureAttribute"/> and the limitations it imposes on the semantics of
    /// delegates so-annotated.</remarks>
    [NoNonVsCapture]
    public delegate TResult VaultQuery<TResource, [VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
       in TResource res, in TAncillary ancillary);
    /// <summary>
    /// Execute a potentially mutating action on the vault
    /// </summary>
    /// <typeparam name="TResource">the type of protected resource you wish to mutate</typeparam>
    /// <param name="res">the protected resource on which you wish to perform a mutation.</param>
    [NoNonVsCapture]
    public delegate void VaultAction<TResource>(ref TResource res);
    /// <summary>
    /// Execute a potentially mutating action on the vault
    /// </summary>
    /// <typeparam name="TResource">the type of protected resource you wish to mutate</typeparam>
    /// <typeparam name="TAncillary">an ancillary type used in the delegate.  must be vault safe, must be passed
    /// by readonly-reference.</typeparam>
    /// <param name="res">the protected resource on which you wish to perform a mutation.</param>
    /// <param name="ancillary">the ancillary object</param>
    /// <remarks>See <see cref="NoNonVsCaptureAttribute"/> and the limitations it imposes on the semantics of
    /// delegates so-annotated.</remarks>
    [NoNonVsCapture]
    public delegate void VaultAction<TResource, [VaultSafeTypeParam] TAncillary>(ref TResource res,
        in TAncillary ancillary);
    /// <summary>
    /// Execute a mixed query: a value is desired and returned, but mutation may also happen to protected resource during the
    /// operation
    /// </summary>
    /// <typeparam name="TResource">The protected resource type</typeparam>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="res">the protected resource</param>
    /// <returns>the result</returns>
    /// <remarks>See <see cref="NoNonVsCaptureAttribute"/> and the limitations it imposes on the semantics of
    /// delegates so-annotated.</remarks>
    [NoNonVsCapture]
    public delegate TResult VaultMixedOperation<TResource, [VaultSafeTypeParam] TResult>(ref TResource res);
    /// <summary>
    /// Execute a mixed query: a value is desired and returned, but mutation may also happen to protected resource during the
    /// operation
    /// </summary>
    /// <typeparam name="TResource">The protected resource type</typeparam>
    /// <typeparam name="TAncillary">an ancillary type to be used in the delegate.  must be vault-safe, must be passed by
    /// readonly-reference.</typeparam>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="res">the protected resource</param>
    /// <param name="a">the ancillary object</param>
    /// <returns>the result</returns>
    /// <remarks>See <see cref="NoNonVsCaptureAttribute"/> and the limitations it imposes on the semantics of
    /// delegates so-annotated.</remarks>
    [NoNonVsCapture]
    public delegate TResult VaultMixedOperation<TResource, [VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
        ref TResource res, in TAncillary a);
    // ReSharper restore TypeParameterCanBeVariant 
    #endregion

    #region Locked Mutable Resource
    /// <summary>
    /// Represents a lock on a resource acquired from a mutable resource vault.  Unlike the <see cref="BasicVault{T}"/>
    /// and it's <see cref="LockedVaultObject{TVault,T}"/>, this lock is taken
    /// on a NON-VAULT-SAFE-TYPE -- such as one guarded by a <see cref="MutableResourceVault{T}"/> or a customized version of the same
    /// <seealso cref="StringBuilderVault"/>.  Access to the mutable resource is not exposed directly by this lock object but rather
    /// can be queried by delegates annotated with the <seealso cref="NoNonVsCaptureAttribute"/> attribute: this prevents the protected mutable
    /// resource from being leaked out or prevents unprotected mutable state from becoming a part of the protected resource.
    /// </summary>
    /// <typeparam name="TVault">The vault type</typeparam>
    /// <typeparam name="TResource">The protected resource type</typeparam>
    /// <remarks>This implementation is returned from vaults that use atomics as their internal synchronization mechanism.
    /// <seealso cref="LockedMonVaultMutableResource{TVault, TResource}"/> which is the locked resource object for the <see cref="MutableResourceMonitorVault{T}"/>,
    /// a mutable resource vault that <seealso cref="System.Threading.Monitor"/> and sync objects for synchronization.</remarks>
    public readonly ref struct LockedVaultMutableResource<TVault, TResource> where TVault : AtomicVault<TResource>
    {
        #region Static Factory
        /// <summary>
        /// This method creates a mutable resource vault.  It is internal because it should be called only by a <see cref="LockedVaultMutableResource{TVault,TResource}"/>
        /// It is not intended to be used directly by client code.
        /// </summary>
        /// <param name="vault">the vault that this locked resource is obtained from</param>
        /// <param name="box">The vault's box object</param>
        /// <returns>the locked mutable resource vault.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        internal static LockedVaultMutableResource<TVault, TResource> CreateLockedResource([NotNull] TVault vault,
            [NotNull] Vault<TResource>.Box box)
        {
            Func<TVault, Vault<TResource>.Box, Vault<TResource>.Box> releaseFunc = AtomicVault<TResource>.ReleaseResourceMethod;
            var temp = new LockedVaultMutableResource<TVault, TResource>(vault, box, releaseFunc);
            return temp;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Execute a query on the mutable resource
        /// </summary>
        /// <typeparam name="TAncillary">an ancillary value to be used in the query</typeparam>
        /// <typeparam name="TResult">the result of the query</typeparam>
        /// <param name="q">the query delegate</param>
        /// <param name="val">the ancillary value</param>
        /// <returns>the result of the delegate execution.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteQuery<[VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>
            ([NotNull] VaultQuery<TResource, TAncillary, TResult> q, in TAncillary val)
        {
            if (q == null) throw new ArgumentNullException(nameof(q));
            return q(in _box.Value, in val);
        }

        /// <summary>
        /// Execute a query on the mutable resource
        /// </summary>
        /// <typeparam name="TResult">the result of the query</typeparam>
        /// <param name="q">the query delegate</param>
        /// <returns>the result of the delegate execution.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteQuery<[VaultSafeTypeParam] TResult>
            ([NotNull] VaultQuery<TResource, TResult> q)
        {
            if (q == null) throw new ArgumentNullException(nameof(q));
            return q(in _box.Value);
        }

        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <param name="action">the action</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction([NotNull] VaultAction<TResource> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            action(ref _box.Value);
        }

        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <typeparam name="TAncillary">an ancillary to be used by the mutation delegate</typeparam>
        /// <param name="action">the mutation action</param>
        /// <param name="ancillaryValue">the ancillary value</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction<[VaultSafeTypeParam] TAncillary>
            ([NotNull] VaultAction<TResource, TAncillary> action, in TAncillary ancillaryValue)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            action(ref _box.Value, in ancillaryValue);
        }

        /// <summary>
        /// Executes a query on the mutable resource, while potentially performing a mutation on it.
        /// </summary>
        /// <typeparam name="TResult">The result object</typeparam>
        /// <param name="mixedOp">The mixed query/mutate delegate</param>
        /// <returns>the result</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteMixedOperation<[VaultSafeTypeParam] TResult>(
            [NotNull] VaultMixedOperation<TResource, TResult> mixedOp)
        {
            if (mixedOp == null) throw new ArgumentNullException(nameof(mixedOp));
            return mixedOp(ref _box.Value);
        }

        /// <summary>
        /// Executes a query on the mutable resource, while potentially performing a mutation on it.
        /// </summary>
        /// <typeparam name="TResult">The result object</typeparam>
        /// <typeparam name="TAncillary">the ancillary object type that should be used by the delegate</typeparam>
        /// <param name="mixedOp">The mixed query/mutate delegate</param>
        /// <param name="ancillary">the ancillary object</param>
        /// <returns>the result</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteMixedOperation<[VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
            [NotNull] VaultMixedOperation<TResource, TAncillary, TResult> mixedOp, in TAncillary ancillary)
        {
            if (mixedOp == null) throw new ArgumentNullException(nameof(mixedOp));
            return mixedOp(ref _box.Value, in ancillary);
        }

        /// <summary>
        /// Returns the locked resource back to the vault whence it came
        /// making it available to other threads
        /// </summary>
        [NoDirectInvoke]
        public void Dispose() => DoDispose();

        /// <summary>
        /// DO NOT CALL EXCEPT IN TWO CIRCUMSTANCES:
        ///    1. Error case when building a custom locked resource
        ///    2. Inside the custom locked resource's dispose method
        /// NEVER CALL ON AN ASSIGNMENT TARGET OF USING MANDATORY
        /// </summary>
        [EarlyRelease]
        public void ErrorCaseReleaseOrCustomWrapperDispose() => DoDispose();

        private void DoDispose()
        {
            Debug.Assert((_disposeMethod == null) == (_box == null));
            Vault<TResource>.Box b = _box;
            Func<TVault, Vault<TResource>.Box, Vault<TResource>.Box> disposeMethod
                = _disposeMethod;
            if (disposeMethod != null)
            {
                var temp = disposeMethod(_vault, b);
                Debug.Assert(temp == null);
            }
        }
        #endregion

        #region Private CTOR
        private LockedVaultMutableResource([NotNull] TVault v, [NotNull] Vault<TResource>.Box b,
           [NotNull]  Func<TVault, Vault<TResource>.Box, Vault<TResource>.Box> disposeMethod)
        {
            Debug.Assert(v != null && b != null && disposeMethod != null);
            _vault = v;
            _box = b;
            _disposeMethod = disposeMethod;
        }
        #endregion

        #region Privates
        private readonly Func<TVault, Vault<TResource>.Box, Vault<TResource>.Box> _disposeMethod;
        private readonly TVault _vault;
        private readonly Vault<TResource>.Box _box;
        #endregion
    } 
    #endregion
}