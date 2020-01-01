using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.Interfaces
{
    [NotVsProtectable]
    internal interface
        IVsDictionaryWrapper<[VaultSafeTypeParam] TKey, [VaultSafeTypeParam] TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        new KvpEnumerator<TKey, TValue> GetEnumerator();
        ImmutableDictionary<TKey, TValue> ToImmutable();
        ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary();
        ImmutableDictionary<TKey, TValue> ToImmutableDictionary([NotNull] EqualityComparer<TKey> comparer);
        ImmutableSortedDictionary<TKey, TValue> ToImmutableSortedDictionary([NotNull] Comparer<TKey> comparer);
    }
}