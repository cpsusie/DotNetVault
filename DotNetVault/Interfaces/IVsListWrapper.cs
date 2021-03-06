﻿using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.Interfaces
{
    [NotVsProtectable]
    internal interface IVsListWrapper<[VaultSafeTypeParam] T> : IReadOnlyList<T>
    {
        new StructEnumeratorWrapper<List<T>.Enumerator, T> GetEnumerator();
        ImmutableList<T> ToImmutableList();
        ImmutableArray<T> ToImmutableArray();
        [Pure]
        int IndexOf(T item);
        [Pure]
        bool Contains(T item);
        void CopyTo(T[] array, int idx);
    }
}