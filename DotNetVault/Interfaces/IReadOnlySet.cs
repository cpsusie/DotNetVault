using System.Collections.Generic;
using DotNetVault.Attributes;

namespace DotNetVault.Interfaces
{
    [NotVsProtectable]
    internal interface IReadOnlySet<T> : IReadOnlyCollection<T>
    {
        bool Contains(T item);
        void CopyTo(T[] array, int arrayIndex);
        bool IsProperSubsetOf(IEnumerable<T> other);
        bool IsProperSupersetOf(IEnumerable<T> other);
        bool IsSubsetOf(IEnumerable<T> other);
        bool IsSupersetOf(IEnumerable<T> other);
        bool Overlaps(IEnumerable<T> other);
        bool SetEquals(IEnumerable<T> other);

    }
}