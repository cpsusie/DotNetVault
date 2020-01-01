using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    //BEGIN VAULT SAFE TYPES (OR TYPES THAT EFFECTIVELY LIE ABOUT IT)
    [VaultSafe]
    public struct Dog
    {
        public string Name => _name ?? "Anonymous";
        public int Age => _age;

        public Dog([NotNull] string name, int age)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(@"Every dog deserves a name that is not empty or just whitespace.",
                    nameof(name));
            if (age < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(age), age, @"Negative ages are non-sensical.");
            }
            _name = name;
            _age = age;
        }

        public override string ToString() => $"Name: {Name}, Age: {Age}";
        

        private readonly string _name;
        private readonly int _age;
    }

    [VaultSafe]
    public sealed class DoggiePair
    {
        public Dog FirstDog { get; }

        public Dog SecondDog { get; }

        public string PairName { get; }
               
        public DoggiePair([NotNull] string pairName, Dog numberOne, Dog numberTwo)
        {
            PairName = pairName ?? throw new ArgumentNullException(nameof(pairName));
            FirstDog = numberOne;
            SecondDog = numberTwo;
        }
    }

    [VaultSafe(5 + 1 == 6)]
    public class LyingRat
    {
        public StringBuilder MutableName { get; set; }
    }

    public readonly struct DefaultVaultSafe : IEquatable<DefaultVaultSafe>, IComparable<DefaultVaultSafe>
    {
        public static DefaultVaultSafe DefaultValue = default;
        public static DefaultVaultSafe CreateValue() => new DefaultVaultSafe(DateTime.Now, Guid.NewGuid());

        public DateTime TimeStamp => _stamp;
        public Guid Identifier => _id;

        private DefaultVaultSafe(DateTime stamp, Guid id)
        {
            _stamp = stamp;
            _id = id;
        }

        public override string ToString() => $"Timestamp: [{_stamp:O}]; id: [{_id}].";

        public static bool operator ==(in DefaultVaultSafe lhs, in DefaultVaultSafe rhs) => lhs._id == rhs._id && lhs._stamp == rhs._stamp;
        public static bool operator !=(in DefaultVaultSafe lhs, in DefaultVaultSafe rhs) => !(lhs == rhs);
        public static bool operator >(in DefaultVaultSafe lhs, in DefaultVaultSafe rhs) => Compare(lhs, rhs) > 0;
        public static bool operator <(in DefaultVaultSafe lhs, in DefaultVaultSafe rhs) => Compare(lhs, rhs) < 0;
        public static bool operator >=(in DefaultVaultSafe lhs, in DefaultVaultSafe rhs) => !(lhs < rhs);
        public static bool operator <=(in DefaultVaultSafe lhs, in DefaultVaultSafe rhs) => !(lhs > rhs);
        public override int GetHashCode() => _id.GetHashCode();
        public override bool Equals(object other) => ((DefaultVaultSafe?) other) == this;
        public bool Equals(DefaultVaultSafe other) => this == other;
        public int CompareTo(DefaultVaultSafe other) => Compare(this, other);

        private static int Compare(in DefaultVaultSafe lhs, in DefaultVaultSafe rhs)
        {
            int dtCompare = lhs._stamp.CompareTo(rhs._stamp);
            return dtCompare == 0 ? lhs._id.CompareTo(rhs._id) : dtCompare;
        }

        private readonly DateTime _stamp;
        private readonly Guid _id;        
    }

    [VaultSafe]
    public sealed class Bobcat
    {
        public string Name => _name;

        public int Age => _age;

        public Bobcat([NotNull] string name, int age)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(@"Every cat deserves a name that is not empty or just whitespace.",
                    nameof(name));
            if (age < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(age), age, @"Negative ages are non-sensical.");
            }
            _name = name;
            _age = age;
        }

        private readonly int _age;
        private readonly string _name;
    }

    //BEGIN NOT VAULT SAFE TYPES 
    public sealed class WouldBeVaultSafeIfSoAnnotated
    {
        public string Name { get; }

        public DateTime Timestamp { get; }

        public WouldBeVaultSafeIfSoAnnotated(string name)
        {
            Timestamp = DateTime.Now;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public WouldBeVaultSafeIfSoAnnotated()
        {
            Timestamp = DateTime.Now;
            Name = "Anonymous";
        }
    }

    [VaultSafe]
    public class CatThatIsntSealed
    {
        public string Name => _name;

        public int Age => _age;

        public CatThatIsntSealed([NotNull] string name, int age)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(@"Every cat deserves a name that is not empty or just whitespace.",
                    nameof(name));
            if (age< 0)
            {
                throw new ArgumentOutOfRangeException(nameof(age), age, @"Negative ages are non-sensical.");
            }
            _name = name;
            _age = age;
        }

        private readonly int _age;
        private readonly string _name;
    }
}
