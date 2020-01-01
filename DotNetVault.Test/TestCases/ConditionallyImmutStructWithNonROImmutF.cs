using System.Collections.Generic;
using System.Collections.Immutable;
using AnalyzerYetAgain.Attributes;

namespace AnalyzerYetAgain.Test.TestCases
{
    [VaultSafe]
    public struct ConditionallyImmutStructWithNonROImmutF
    {
        public int Count => _lookup.Count;

        public double this[string key] => _lookup[key];

        public ConditionallyImmutStructWithNonROImmutF(IEnumerable<KeyValuePair<string, double>> items)
        {
            _lookup = ImmutableDictionary<string, double>.Empty.AddRange(items);
        }

        private ImmutableDictionary<string, double> _lookup;
    }
}
