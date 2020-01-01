using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using JetBrains.Annotations;

namespace DotNetVault.VsWrappers
{
    /// <summary>
    /// A mutable wrapper around a object of type <see cref="List{T}"/> where T is a vault-safe type
    /// allowing it to be used to in the query/action delegates
    /// </summary>
    /// <typeparam name="T">the vault safe type of the list.</typeparam>
    [NotVsProtectable]
    [VaultSafe(true)]
    public sealed class VsListWrapper<[VaultSafeTypeParam] T> : IVsListWrapper<T>
    {
        #region Factory Methods and Conversion Op
        /// <summary>
        /// Creates mutable wrapper around a object of type <see cref="List{T}"/> where T is a vault-safe type
        /// allowing it to be used to in the query/action delegates
        /// </summary>
        /// <param name="wrapMe">the list to wrap</param>
        /// <typeparam name="TQ">vault-safe type of the list</typeparam>
        /// <returns>the wrapper</returns>
        /// <exception cref="ArgumentNullException"><paramref name="wrapMe"/> was null</exception>
        public static VsListWrapper<TQ> FromList<TQ>([NotNull] List<TQ> wrapMe) =>
            new VsListWrapper<TQ>(wrapMe ?? throw new ArgumentNullException(nameof(wrapMe)));

        /// <summary>
        /// implicitly converts a list of type <see cref="List{T}"/> to a wrapper that can be used in one
        /// of the action/query/mixedaction delegates
        /// </summary>
        /// <param name="convertMe">the list to wrap</param>
        /// <returns>the wrapper</returns>
        /// <exception cref="ArgumentNullException"><paramref name="convertMe"/> was null.</exception>
        public static implicit operator VsListWrapper<T>([NotNull] List<T> convertMe) => FromList(convertMe);
        #endregion

        #region Public Properties
        /// <inheritdoc />
        public int Count => _wrapped.Count;
        /// <inheritdoc />
        public T this[int index] => _wrapped[index]; 
        #endregion

        #region Private CTOR
        private VsListWrapper([NotNull] List<T> wrapMe) => _wrapped = wrapMe; 
        #endregion

        #region Public Methods
        /// <inheritdoc />
        public StructEnumeratorWrapper<List<T>.Enumerator, T> GetEnumerator() =>
            StructEnumeratorWrapper<List<T>.Enumerator, T>.CreateWrapper<List<T>.Enumerator, T>(_wrapped.GetEnumerator());
        /// <summary>
        /// Convert object into an immutable list of T
        /// </summary>
        /// <returns>an immutable list of T</returns>
        public ImmutableList<T> ToImmutableList() => ImmutableList.Create(_wrapped.ToArray());
        /// <summary>
        /// Convert object into an immutable array of T
        /// </summary>
        /// <returns>an array list of T</returns>
        public ImmutableArray<T> ToImmutableArray() => ImmutableArray.Create(_wrapped.ToArray());
        /// <inheritdoc />
        public int IndexOf(T item) => _wrapped.IndexOf(item);
        /// <inheritdoc />
        public bool Contains(T item) => _wrapped.Contains(item);
        /// <inheritdoc />
        public void CopyTo(T[] array, int idx) => _wrapped.CopyTo(array, idx); 
        #endregion

        #region Explicit Interface Impl
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region Wrapped Privates
        [NotNull] private readonly List<T> _wrapped; 
        #endregion
    }
}