using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using DotNetVault.VsWrappers;

namespace DotNetVault.Interfaces
{
    [NotVsProtectable]
    internal interface IVsEnumerableWrapper<[VaultSafeTypeParam] T> : IEnumerable<T>
    {
        /// <summary>
        /// Get an enumerator to iterate elements
        /// </summary>
        /// <returns>an enumerator</returns>
        new StandardEnumerator<T> GetEnumerator();
        /// <summary>
        /// Copy contents to immutable array
        /// </summary>
        /// <returns>an immutable array</returns>
        ImmutableArray<T> ToImmutableArray();
        /// <summary>
        /// Copy contents to immutable list
        /// </summary>
        /// <returns>an immutable list</returns>
        ImmutableList<T> ToImmutableList();
    }
}
