using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.Interfaces
{
    [NotVsProtectable]
    internal interface IVsArrayWrapper<[VaultSafeTypeParam] T> : IReadOnlyList<T>
    {
        /// <summary>
        /// Get an enumerator to iterate the elements
        /// </summary>
        /// <returns></returns>
        new ArrayEnumeratorWrapper<T> GetEnumerator();
        /// <summary>
        /// Copy elements to an immutable array
        /// </summary>
        /// <returns>immutable array</returns>
        [Pure]
        ImmutableArray<T> ToImmutable();
        /// <summary>
        /// Find the index of the first item with value equal to that of
        /// by <paramref name="item"/>.
        /// </summary>
        /// <param name="item">the item to find</param>
        /// <returns>the index if found, -1 otherwise</returns>
        [Pure]
        int IndexOf(T item);
        /// <summary>
        /// Query whether the collection contains the specified value
        /// </summary>
        /// <param name="item">the query item</param>
        /// <returns>true if the item contains the value, false otherwise</returns>
        [Pure]
        bool Contains(T item);
        /// <summary>
        /// Copy the elements of the array to the specified array
        /// </summary>
        /// <param name="array">array to copy to</param>
        /// <param name="idx">the index</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> was null</exception>
        /// <exception cref="ArgumentOutOfRangeException">wouldn't fit as specified</exception>
        void CopyTo(T[] array, int idx);
    }
} 