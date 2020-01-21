using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomVaults;
using DotNetVault.Interfaces;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.CustomVaultExamples.CustomLockedResources
{
    /// <summary>
    /// This is, with its companion <see cref="StringBuilderVault"/> shows how one might
    /// enhance the functionality provided by <see cref="MutableResourceVault{T}"/> by providing a
    /// locked resource object that provides an interface more similar to the protected resource than
    /// the one provided by <see cref="LockedVaultMutableResource{TVault,TResource}"/> as used by
    /// <see cref="MutableResourceVault{T}"/>.  It wraps the <see cref="LockedVaultMutableResource{TVault,TResource}"/>
    /// and exposes an interface similar to the one expected by users of the <see cref="StringBuilder"/> type.
    ///
    /// All such custom locked resources:
    ///  1- should be ref structs (to prevent them from being stored on the heap either from boxing or as field
    ///     of a reference type and to prevent them from being stored in static memory),
    ///  2- should have a public <see cref="Dispose"/> method with the same signature as this type which
    ///     disposes the <see cref="LockedVaultMutableResource{TVault,TResource}"/> it wraps, 
    ///  3- should expose an interface similar to that of the protected resource, except all parameters
    ///     passed to the methods and retrieved from the methods MUST be Vault-Safe. 
    ///  4- should have a private constructor that accepts the appropriate <see cref="LockedVaultMutableResource{TVault,TResource}"/> to wrap,
    ///  5- should be in the same assembly as your custom <see cref="IVault"/> object,
    ///  6- should expose an internal factory method, used only by your <see cref="IVault"/> object's public
    ///     Lock() and SpinLock() overloads (all of which should have return type of this type,
    ///     annotated with the <see cref="UsingMandatoryAttribute"/>. 
    /// </summary>
    /// <remarks>For performance reasons, you may also want to expose the wrapped <see cref="LockedVaultMutableResource{TVault,TResource}"/>'s
    /// ExecuteQuery, PerformAction and ExecuteMixedOperation methods-- if you will be calling this api repeatedly (say in a loop), you may wish
    /// to avoid the overhead of a delegate invocation per call.
    /// </remarks>
    /// <remarks>
    /// For some types, you may need to substitute VaultSafe alternatives (such as <see cref="ImmutableArray{T}"/> for <see cref="List{T}"/>)
    /// or you may not be able to provide the entire API.
    /// </remarks>
    public readonly ref struct LockedStringBuilder
    {
        #region Static Factory Method
        internal static LockedStringBuilder CreateLockedResource(
            LockedVaultMutableResource<MutableResourceVault<StringBuilder>, StringBuilder> me)
                => new LockedStringBuilder(me); 
        #endregion

        #region Public Properties
        /// <summary>
        /// <seealso cref="StringBuilder.Length"/>
        /// </summary>
        public int Length => _resource.ExecuteQuery((in StringBuilder sb) => sb.Length);
        /// <summary>
        /// <see cref="StringBuilder.MaxCapacity"/>
        /// </summary>
        public int MaxCapacity => _resource.ExecuteQuery((in StringBuilder sb) => sb.MaxCapacity);

        /// <summary>
        /// <seealso cref="StringBuilder.Capacity"/>
        /// </summary>
        public int Capacity
        {
            get => _resource.ExecuteQuery((in StringBuilder sb) => sb.Capacity);
            set => _resource.ExecuteAction((ref StringBuilder sb) => sb.Capacity = value);
        }

        /// <summary>
        /// <seealso cref="StringBuilder"/>
        /// </summary>
        /// <param name="idx">index of the character you wish to access</param>
        /// <returns>the character stored at the specified index</returns>
        public char this[int idx]
        {
            get => _resource.ExecuteQuery((in StringBuilder sb) => sb[idx]);
            set => _resource.ExecuteAction((ref StringBuilder sb) => sb[idx] = value);
        } 
        #endregion

        #region Private CTOR
        /// <summary>
        /// Private CTOR
        /// </summary>
        /// <param name="wrappedLockedResource">The resource from <see cref="LockedVaultMutableResource{TVault,TResource}"/> that this object will be wrapping
        /// and assuming disposal obligations</param>
        private LockedStringBuilder(
            LockedVaultMutableResource<MutableResourceVault<StringBuilder>, StringBuilder> wrappedLockedResource) =>
            _resource = wrappedLockedResource;
        #endregion

        #region Public Methods
        /// <summary>
        /// Query whether the stringbuilder contains the specified resource
        /// </summary>
        /// <param name="c">the character whose presence vel-non in the <see cref="StringBuilder"/>
        /// you wish to determine</param>
        /// <returns>true if found, false otherwise</returns>
        public bool Contains(char c) => IndexOf(c) > -1;
        
        /// <summary>
        /// Release the lock on the <see cref="StringBuilder"/>, returning it to the vault
        /// for use on other threads
        /// </summary>
        [NoDirectInvoke]
        [EarlyReleaseJustification(EarlyReleaseReason.CustomWrapperDispose)]
        public void Dispose() => _resource.ErrorCaseReleaseOrCustomWrapperDispose();

        /// <summary>
        /// Append the specified text to the end of the <see cref="StringBuilder"/>
        /// </summary>
        /// <param name="appendMe">the text to append</param>
        public void Append([NotNull] string appendMe) =>
            _resource.ExecuteAction((ref StringBuilder sb) => sb.Append(appendMe));
        /// <summary>
        /// Append the specified text followed by a "newline" character to the <see cref="StringBuilder"/>
        /// </summary>
        /// <param name="appendMe">the string to append</param>
        public void AppendLine([NotNull] string appendMe) =>
            _resource.ExecuteAction((ref StringBuilder sb) => sb.AppendLine(appendMe));
        /// <summary>
        /// Empties the <see cref="StringBuilder"/>
        /// </summary>
        public void Clear() => _resource.ExecuteAction((ref StringBuilder sb) => sb.Clear());
        /// <summary>
        /// Retrieve the string representation of the protected object in its current state
        /// </summary>
        /// <returns>string rep of protected object</returns>
        public new string ToString() => _resource.ExecuteQuery((in StringBuilder sb) => sb.ToString());

        /// <summary>
        /// Find the first index of the specified character.
        /// </summary>
        /// <param name="c">the character whose index you seek</param>
        /// <returns>the first index whereat the specified character is found, if found; -1 otherwise</returns>
        public int IndexOf(char c) => _resource.ExecuteQuery((in StringBuilder sb) =>
        {
            for (int i = 0; i < sb.Length; ++i)
            {
                if (sb[i] == c)
                    return i;
            }

            return -1;
        });

        /// <summary>
        /// Copies the characters stored in the string builder to a new <see cref="ImmutableArray{T}"/> along
        /// with the characters in the specified <see cref="ImmutableArray{T}"/> starting at <paramref name="toMe"/>'s
        /// <paramref name="targetStartIdx"/> index and the <see cref="StringBuilder"/>'s <paramref name="sbStartIdx"/> index
        /// and continuing for the next <paramref name="numChars"/> characters in the <see cref="StringBuilder"/>
        /// </summary>
        /// <returns>A new mutable array with values as if the copy had been written to <paramref name="toMe"/> in the manner specified.</returns>
        /// <remarks>Obviously, does not actually change the contents of <paramref name="toMe"/></remarks>
        public ImmutableArray<char> CopyTo(ImmutableArray<char> toMe, int targetStartIdx, int sbStartIdx, int numChars) => _resource.ExecuteMixedOperation((ref StringBuilder sb) =>
        {
            var ret = toMe.InsertRange(targetStartIdx, sb.ToString(sbStartIdx, numChars));
            return ret;
        });
        #endregion

        #region Wrapped Accessors for access via delegates
        /// <summary>
        /// Execute a query on the mutable resource
        /// </summary>
        /// <typeparam name="TAncillary">an ancillary value to be used in the query</typeparam>
        /// <typeparam name="TResult">the result of the query</typeparam>
        /// <param name="q">the query delegate</param>
        /// <param name="val">the ancillary value</param>
        /// <returns>the result of the delegate execution.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteQuery<[VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
            [NotNull] VaultQuery<StringBuilder, TAncillary, TResult> q, in TAncillary val) =>
            _resource.ExecuteQuery(q, in val);
        /// <summary>
        /// Execute a query on the mutable resource
        /// </summary>
        /// <typeparam name="TResult">the result of the query</typeparam>
        /// <param name="q">the query delegate</param>
        /// <returns>the result of the delegate execution.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteQuery<[VaultSafeTypeParam] TResult>([NotNull] 
            VaultQuery<StringBuilder, TResult> q) => _resource.ExecuteQuery(q);
        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <param name="action">the action</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction([NotNull] VaultAction<StringBuilder> action) 
            => _resource.ExecuteAction(action);
        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <typeparam name="TAncillary">an ancillary to be used by the mutation delegate</typeparam>
        /// <param name="action">the mutation action</param>
        /// <param name="ancillaryValue">the ancillary value</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction<[VaultSafeTypeParam] TAncillary>(
            [NotNull] VaultAction<StringBuilder, TAncillary> action, in TAncillary ancillaryValue)
            => _resource.ExecuteAction(action, in ancillaryValue);
        /// <summary>
        /// Executes a query on the mutable resource, while potentially performing a mutation on it.
        /// </summary>
        /// <typeparam name="TResult">The result object</typeparam>
        /// <param name="mixedOp">The mixed query/mutate delegate</param>
        /// <returns>the result</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteMixedOperation<[VaultSafeTypeParam] TResult>(
            [NotNull] VaultMixedOperation<StringBuilder, TResult> mixedOp) =>
                _resource.ExecuteMixedOperation(mixedOp);
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
            [NotNull] VaultMixedOperation<StringBuilder, TAncillary, TResult> mixedOp, in TAncillary ancillary)
            => _resource.ExecuteMixedOperation(mixedOp, in ancillary);
        #endregion

        #region Privates
        private readonly LockedVaultMutableResource<MutableResourceVault<StringBuilder>, StringBuilder> _resource; 
        #endregion
    }
}
