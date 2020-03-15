using System;
using System.Diagnostics;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomVaults;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.LockedResources
{
    /// <summary>
    /// Represents a lock on a resource acquired from a mutable resource vault.  Unlike the <see cref="BasicMonitorVault{T}"/>
    /// and it's <see cref="LockedMonVaultObject{TVault,T}"/>, this lock is taken
    /// on a NON-VAULT-SAFE-TYPE -- such as one guarded by a <see cref="MutableResourceMonitorVault{T}"/> or a customized version of the same
    /// <seealso cref="StringBuilderVault"/>.  Access to the mutable resource is not exposed directly by this lock object but rather
    /// can be queried by delegates annotated with the <seealso cref="NoNonVsCaptureAttribute"/> attribute: this prevents the protected mutable
    /// resource from being leaked out or prevents unprotected mutable state from becoming a part of the protected resource.
    /// </summary>
    /// <typeparam name="TVault">The vault type</typeparam>
    /// <typeparam name="TResource">The protected resource type</typeparam>
    /// <remarks>This implementation is returned from vaults that use  <seealso cref="System.Threading.Monitor"/> and sync objects for synchronization mechanism.
    /// <seealso cref="LockedVaultMutableResource{TVault,TResource}"/> which is the locked resource object for the <see cref="MutableResourceVault{T}"/>,
    /// a mutable resource vault that uses atomics for synchronization.</remarks>
    public readonly ref struct LockedMonVaultMutableResource<TVault, TResource> where TVault : MonitorVault<TResource>
    {
        #region Factory Method
        internal static LockedMonVaultMutableResource<TVault, TResource> CreateLockedResource([NotNull] TVault vault,
            [NotNull] Vault<TResource>.Box box)
        {
            Func<MonitorVault<TResource>, Vault<TResource>.Box, Vault<TResource>.Box> releaseFunc = MonitorVault<TResource>.ReleaseResourceMethod;
            return new LockedMonVaultMutableResource<TVault, TResource>(vault, box, releaseFunc);
        } 
        #endregion

        #region Private CTOR
        private LockedMonVaultMutableResource([NotNull] TVault v, [NotNull] Vault<TResource>.Box b,
            [NotNull] Func<MonitorVault<TResource>, Vault<TResource>.Box, Vault<TResource>.Box> disposeMethod)
        {
            Debug.Assert(v != null && b != null && disposeMethod != null);
            _box = b;
            _disposeMethod = disposeMethod;
            _flag = new DisposeFlag();
            _vault = v;
        }
        #endregion

        #region Public Actions and Queries
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

        #endregion

        #region Dispose / Release Methods
        /// <summary>
        /// Returns the locked resource back to the vault whence it came
        /// making it available to other threads
        /// </summary>
        [NoDirectInvoke]
        public void Dispose() => DoDispose(true);

        /// <summary>
        /// DO NOT CALL EXCEPT IN TWO CIRCUMSTANCES:
        ///    1. Error case when building a custom locked resource
        ///    2. Inside the custom locked resource's dispose method
        /// NEVER CALL ON AN ASSIGNMENT TARGET OF USING MANDATORY
        /// </summary>
        [EarlyRelease]
        public void ErrorCaseReleaseOrCustomWrapperDispose() => DoDispose(true);

        /// <summary>
        /// release the lock and return the protected resource to vault for use by others
        /// </summary>
        private void DoDispose(bool _)
        {
            if (_flag?.TrySet() == true)
            {
                Vault<TResource>.Box b = _box;
                // ReSharper disable once RedundantAssignment DEBUG vs RELEASE
                Vault<TResource>.Box ok = _disposeMethod(_vault, b);
                Debug.Assert(ok == null);
            }
        }
        #endregion

        #region Privates
        private readonly Vault<TResource>.Box _box;
        private readonly TVault _vault;
        private readonly Func<MonitorVault<TResource>, Vault<TResource>.Box, Vault<TResource>.Box> _disposeMethod;
        private readonly DisposeFlag _flag; 
        #endregion
    }
}
