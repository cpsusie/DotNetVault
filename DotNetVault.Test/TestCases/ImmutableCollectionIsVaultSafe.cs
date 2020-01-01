using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public sealed class ImmutableCollectionIsVaultSafe
    {
        public string LookupName => _dictionaryName;

        public DateTime this[string key] => _timestampDict[key];

        public ImmutableCollectionIsVaultSafe(IEnumerable<KeyValuePair<string, DateTime>> kvps) : this(kvps,
            DefaultDictionaryName) {}

        public ImmutableCollectionIsVaultSafe(IEnumerable<KeyValuePair<string, DateTime>> kvps, string name) : this()
        {
            _timestampDict = ImmutableDictionary<string, DateTime>.Empty.AddRange(kvps);
            _dictionaryName = !string.IsNullOrWhiteSpace(name)
                ? name
                : throw new ArgumentException(@"Parameter may not be null empty or just whitespace.", 
                    nameof(name));
        }

        public ImmutableCollectionIsVaultSafe()
        {
            _timestampDict = ImmutableDictionary<string, DateTime>.Empty.Add("NOW", DateTime.Now);
        }

        public ImmutableCollectionIsVaultSafe(string name) : this()
        {
            _dictionaryName = !string.IsNullOrWhiteSpace(name)
                ? name
                : throw new ArgumentException(@"Parameter may not be null empty or just whitespace.",
                    nameof(name));}

        public (bool HasEntry, DateTime TimeStamp) TryGetTimestamp(string key)
        {
            bool hasEntry = _timestampDict.TryGetValue(key, out DateTime ts);
            return (hasEntry, ts);
        }

        public ImmutableArray<KeyValuePair<string, DateTime>> ToArray() => _timestampDict.ToImmutableArray();

        private const string DefaultDictionaryName = "DEFAULT";
        private readonly string _dictionaryName = DefaultDictionaryName;
        private readonly ImmutableDictionary<string, DateTime> _timestampDict;
    }
}
