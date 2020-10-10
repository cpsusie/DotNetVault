using System;
using DotNetVault.Attributes;
using HpTimesStamps;
using JetBrains.Annotations;

namespace DotNetVaultQuickStart
{
    
    [VaultSafe]
    public readonly struct DogActionRecord : IEquatable<DogActionRecord>, IComparable<DogActionRecord>
    {
        public DateTime Timestamp { get; }

        [NotNull] public string Action => _action ?? string.Empty;

        public DogActionRecord([NotNull] string action)
        {
            Timestamp = TimeStampSource.Now;
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override string ToString() => $"At [{Timestamp:O}], the following DogAction occured: [{Action}]";

        public static bool operator ==(in DogActionRecord lhs, in DogActionRecord rhs) =>
            lhs.Timestamp == rhs.Timestamp && string.Equals(lhs.Action, rhs.Action, StringComparison.Ordinal);
        public static bool operator !=(in DogActionRecord lhs, in DogActionRecord rhs) => !(lhs == rhs);
        public override int GetHashCode() => Timestamp.GetHashCode();
        public override bool Equals(object obj) => obj is DogActionRecord dar && dar == this;
        public bool Equals(DogActionRecord other) => this == other;
        public int CompareTo(DogActionRecord other) => Compare(in this, in other);
        public static bool operator >(in DogActionRecord lhs, in DogActionRecord rhs)
            => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in DogActionRecord lhs, in DogActionRecord rhs)
            => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in DogActionRecord lhs, in DogActionRecord rhs)
            => !(lhs < rhs);
        public static bool operator <=(in DogActionRecord lhs, in DogActionRecord rhs)
            => !(lhs > rhs);

        private static int Compare(in DogActionRecord lhs, in DogActionRecord rhs)
        {
            int ret;
            int tsComparison = lhs.Timestamp.CompareTo(rhs.Timestamp);
            ret = tsComparison != 0
                ? tsComparison
                : string.Compare(lhs.Action, rhs.Action, StringComparison.Ordinal);
            return ret;
        }

        private readonly string _action;
    }
}