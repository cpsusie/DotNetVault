using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using JetBrains.Annotations;

namespace DotNetVault.VsWrappers
{
    /// <summary>
    /// A mutable wrapper around a object of type <see cref="IEnumerable{T}"/> where T is a vault-safe type
    /// allowing it to be used to in the query/action delegates
    /// </summary>
    /// <typeparam name="T">the vault safe type of the enumerator.</typeparam>
    [NotVsProtectable]
    [VaultSafe(true)]
    public sealed class VsEnumerableWrapper<[VaultSafeTypeParam] T> : IVsEnumerableWrapper<T>
    {
        #region Factory Method
        /// <summary>
        /// Convert an enumerator of a vault-safe type into a wrapper where TQ is a vault-safe type
        /// allowing it to be used to in the query/action delegates
        /// </summary>
        /// <typeparam name="TQ">the vault-safe type</typeparam>
        /// <param name="toWrap">the object to wrap</param>
        /// <returns>the wrapper</returns>
        /// <exception cref="ArgumentNullException"><paramref name="toWrap"/> was null.</exception>
        public static VsEnumerableWrapper<TQ> FromIEnumerable<[VaultSafeTypeParam] TQ>([NotNull] IEnumerable<TQ> toWrap)
            => new VsEnumerableWrapper<TQ>(toWrap ?? throw new ArgumentNullException(nameof(toWrap)));
        #endregion

        #region CTOR
        private VsEnumerableWrapper([NotNull] IEnumerable<T> wrapMe) => _wrapped = wrapMe;
        #endregion

        #region Methods
        /// <inheritdoc />
        public StandardEnumerator<T> GetEnumerator() => StandardEnumerator<T>.CreateStandardEnumerator(_wrapped.GetEnumerator());
        /// <inheritdoc />
        public ImmutableArray<T> ToImmutableArray() => ImmutableArray.Create(_wrapped.ToArray());
        /// <inheritdoc />
        public ImmutableList<T> ToImmutableList() => ImmutableList.Create(_wrapped.ToArray());
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region Fields
        private readonly IEnumerable<T> _wrapped; 
        #endregion
    }
}
