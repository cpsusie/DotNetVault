using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.Interfaces
{
    [NotVsProtectable]
    internal interface ISetWrapper<[VaultSafeTypeParam] T> : IReadOnlySet<T>
    {
        new StandardEnumerator<T> GetEnumerator();
        ImmutableHashSet<T> ToImmutableHashSet();
        ImmutableSortedSet<T> ToImmutableSortedSet();
        ImmutableHashSet<T> ToImmutableHashSet([NotNull] EqualityComparer<T> comparer);
        ImmutableSortedSet<T> ToImmutableSortedSet([NotNull] Comparer<T> comparer);
    }
}