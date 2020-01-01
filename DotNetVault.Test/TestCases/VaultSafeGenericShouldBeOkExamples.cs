using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public readonly struct CrazyLegs<[VaultSafeTypeParam] T>
    {
        public DateTime TimeStamp { get; }

        public T Value { get; }

        public CrazyLegs(T value)
        {
            if (ReferenceEquals(value, null)) throw new ArgumentNullException(nameof(value));
            Value = value;
            TimeStamp = DateTime.Now;

        }
    }

    [VaultSafe]
    public sealed class Holder<T> where T : unmanaged
    {
        public T Value { get; }

        public string Name { get; }

        public Holder(string name, T value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
        }
    }

    [VaultSafe]
    public sealed class BoxedGuid
    {
        public OwnedByGuidHolder MySpecialLittleDoohickey { get; } = new OwnedByGuidHolder(8, TimeSpan.FromMinutes(3.4));
        public Guid Id { get; }
        public BoxedGuid() => Id = Guid.NewGuid();
    }

    [VaultSafe]
    public struct OwnedByGuidHolder
    {
        public int Name { get;
            // set;
            }

        public TimeSpan Length {get;// set;
                                    }

        public OwnedByGuidHolder(int name, TimeSpan length)
        {
            Name = name;
            Length = length;
        }
    }
}
