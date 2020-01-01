using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    //Should fail.  Unlike value types, reference types may not have non-readonly fields, 
    //even if the field is an Immutable type.  Contrast with nearly identical struct example.
    [VaultSafe]
    public sealed class ConditionallyImmutClassWithNonROImmutF
    {
        public int Count => _lookup.Count;

        public double this[string key] => _lookup[key];

        public ConditionallyImmutClassWithNonROImmutF(IEnumerable<KeyValuePair<string, double>> items)
        {
            _lookup = ImmutableDictionary<string, double>.Empty.AddRange(items);
        }

        private ImmutableDictionary<string, double> _lookup;
    }
}