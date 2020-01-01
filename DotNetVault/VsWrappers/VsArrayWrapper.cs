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
    /// A mutable wrapper around a object of type array of <typeparamref name="T"/> where T is a vault-safe type
    /// allowing it to be used to in the query/action delegates
    /// </summary>
    /// <typeparam name="T">the vault safe type of the array.</typeparam>
    [NotVsProtectable]
    [VaultSafe(true)]
    public sealed class VsArrayWrapper<[VaultSafeTypeParam] T> : IVsArrayWrapper<T>
    {
        #region Static factory method and conversion op
        /// <summary>
        /// Creates mutable wrapper around a object of type array of <typeparamref name="T"/> where T is a vault-safe type
        /// allowing it to be used to in the query/action delegates
        /// </summary>
        /// <param name="items">the list to wrap</param>
        /// <typeparam name="TQ">vault-safe type of the array</typeparam>
        /// <returns>the wrapper</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> was null</exception>
        public static VsArrayWrapper<TQ> CreateArrayWrapper<[VaultSafeTypeParam] TQ>([NotNull] TQ[] items) =>
            new VsArrayWrapper<TQ>(items ?? throw new ArgumentNullException(nameof(items)));
        /// <summary>
        /// Implicitly convert an array into a wrapper allowing it to be used to in the query/action delegates
        /// </summary>
        /// <param name="arr">the array</param>
        /// <exception cref="ArgumentNullException"><paramref name="arr"/> was null.</exception>
        public static implicit operator VsArrayWrapper<T>([NotNull] T[] arr) => CreateArrayWrapper(arr);
        #endregion

        #region Properties
        /// <inheritdoc />
        public int Count => _array.Length;
        /// <inheritdoc />
        public T this[int index] => _array[index];
        /// <summary>
        /// Gets the total number of elements in all the dimensions of the System.Array.
        /// </summary>
        public int Length => _array.Length;
        /// <summary>
        /// Gets the total number of elements in all the dimensions of the System.Array.
        /// </summary>
        public long LongLength => _array.LongLength;
        #endregion

        #region Private cTOR
        private VsArrayWrapper([NotNull] T[] wrapMe) => _array = wrapMe;
        #endregion

        #region Public Methods
        /// <inheritdoc />
        public ArrayEnumeratorWrapper<T> GetEnumerator() => ArrayEnumeratorWrapper<T>.CreateArrayEnumeratorWrapper(_array);
        /// <inheritdoc />
        public ImmutableArray<T> ToImmutable() => ImmutableArray.Create(_array);
        /// <inheritdoc />
        public int IndexOf(T item) => Array.IndexOf(_array, item);
        /// <inheritdoc />
        public bool Contains(T item) => IndexOf(item) > -1;
        /// <inheritdoc />
        public void CopyTo(T[] array, int idx) => _array.CopyTo(array, idx);
        #endregion

        #region Explicit implementation
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region Privates
        private readonly T[] _array; 
        #endregion
    }
}