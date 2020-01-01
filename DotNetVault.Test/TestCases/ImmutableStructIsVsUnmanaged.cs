using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public struct ImmutableStructIsVaultSafe<T> where T : unmanaged
    {
        public readonly string Name => _name ?? string.Empty;
        public readonly T this[int index] => _immutableArray[index];
        public readonly int Count => _immutableArray.Length;

        public ImmutableStructIsVaultSafe(IEnumerable<T> items, string name)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var temp = ImmutableArray<T>.Empty;
            _immutableArray = temp.AddRange(items);
            _name = name ?? string.Empty;
        }

        private readonly ImmutableArray<T> _immutableArray;
        private readonly string _name;
    }
}