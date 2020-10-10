using System;
using System.Diagnostics.CodeAnalysis;
using DotNetVault.Attributes;
using DotNetVault.Vaults;

namespace ExampleCodePlayground
{
    [VaultSafe]
    public struct BigStruct : IEquatable<BigStruct>, IComparable<BigStruct>
    {
        public static ref readonly BigStruct DefaultValue => ref TheDefaultValue;
        public readonly bool IsDefault => this == DefaultValue;

        public Guid First
        {
            readonly get => _first;
            set => _first = value;
        }

        
        public Guid Second {  get; set; }
        public Guid Third { get; set; }
        public Guid Fourth { get; set; }

        [JetBrains.Annotations.NotNull]
        public string Name
        {
            readonly get => _name ?? string.Empty;
            set => _name = value ?? throw new ArgumentNullException(nameof(value));
        }

        public BigStruct(in Guid first, in Guid second, in Guid third, in Guid fourth, [JetBrains.Annotations.NotNull] string name)
        {
            _first = first;
            Second = second;
            Third = third;
            Fourth = fourth;
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public static bool operator ==(in BigStruct lhs, in BigStruct rhs) 
            => lhs._first == rhs._first && lhs.Second == rhs.Second &&
               lhs.Third == rhs.Third && lhs.Fourth == rhs.Fourth && 
                StringComparer.Ordinal.Equals(lhs.Name, rhs.Name);
        public static bool operator !=(in BigStruct lhs, in BigStruct rhs) => !(lhs == rhs);
        public static bool operator >(in BigStruct lhs, in BigStruct rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in BigStruct lhs, in BigStruct rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator >=(in BigStruct lhs, in BigStruct rhs) => !(lhs < rhs);
        public static bool operator <=(in BigStruct lhs, in BigStruct rhs) => !(lhs > rhs);
        public static int Compare(in BigStruct lhs, in BigStruct rhs)
        {
            int compareRes = lhs._first.CompareTo(rhs._first);
            if (compareRes != 0) return compareRes;
            
            compareRes = lhs.Second.CompareTo(rhs.Second);
            if (compareRes != 0) return compareRes;
            
            compareRes = lhs.Third.CompareTo(rhs.Third);
            if (compareRes != 0) return compareRes;
            
            compareRes = lhs.Fourth.CompareTo(rhs.Fourth);
            if (compareRes != 0) return compareRes;

            return StringComparer.Ordinal.Compare(lhs.Name, rhs.Name);
        }
        public override readonly bool Equals(object other) => other is BigStruct bs && bs == this;
        public readonly bool Equals(BigStruct other) => other == this;
        public override readonly string ToString() => "First: [" + _first + "]; Second: [" + Second + "]; Third: [" +
                                                      Third + "]; Fourth: [" + Fourth + "].";
        public readonly int CompareTo(BigStruct other) => Compare(in this, in other);
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override readonly int GetHashCode()
        {
            int hash = _first.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ Second.GetHashCode();
                hash = (hash * 397) ^ Third.GetHashCode();
                hash = (hash * 397) ^ Fourth.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
            }
            return hash;
        }

        private string _name;
        private Guid _first;
        private static readonly BigStruct TheDefaultValue = default;
    }

static class BigStructVaultExample
{
    public static void DemonstrateReadOnlyLock()
    {
        using var lck = BigStructVault.RoLock();
        Console.WriteLine($"Name: {lck.Value.Name}; First Guid: {lck.Value.First}.");

        //following line will not compile: cannot perform write operation with readonly lock
        //lck.Value.Fourth = Guid.NewGuid();

        //Note that THIS is ok: it is a totally independent deep copy (probably not something you want to do frequently)
        BigStruct deepCopy = lck.Value;
        if (deepCopy != lck.Value) throw new Exception("Should be equal!");

        deepCopy.Name = "Steve";
        if (deepCopy == lck.Value) throw new Exception("Should not be equal!");

        Console.WriteLine($"Lock resource name: {lck.Value.Name}; Deep copy name: {deepCopy.Name}");
        Console.WriteLine($"Lock resource object stringified without defensive copy: {lck.Value}");

    }
    public static void DemonstrateWriteLock()
    {
        {
            using var lck = BigStructVault.Lock();
            lck.Value.Name = "Tamara";
        }
        using var roLck = BigStructVault.RoLock();
        Console.WriteLine($"Changed name to: {roLck.Value.Name}");
    }
    public static void DemonstrateUpgradableReadOnlyLock()
    {
        using var upgrRoLck = BigStructVault.UpgradableRoLock();
        if (upgrRoLck.Value.Name == "Tamara") //upgrade to writable lock if and only if name exactly equals "Tamara"
        {
            Console.WriteLine("Name exactly equal to Tamara .... adding smith");
            using var rwLock = upgrRoLck.Lock(); //upgrades the lock for the scope of if block
            rwLock.Value.Name += " Smith";
        } //writable lock released here, still have ro lock
        else
        {
            Console.WriteLine("Name not exactly equal to Tamara ... not upgrading ro lock.");
        }
        Console.WriteLine($"Print name now: {upgrRoLck.Value.Name}");
    }
    public static void RunDemo()
    {
        Console.WriteLine($"Calling {nameof(DemonstrateReadOnlyLock)}: ");
        DemonstrateReadOnlyLock();
        Console.WriteLine($"Done {nameof(DemonstrateReadOnlyLock)}");
        Console.WriteLine();

        Console.WriteLine($"Calling {nameof(DemonstrateWriteLock)}: ");
        DemonstrateWriteLock();
        Console.WriteLine($"Done {nameof(DemonstrateWriteLock)}");
        Console.WriteLine();

        Console.WriteLine($"Calling {nameof(DemonstrateUpgradableReadOnlyLock)} first time: ");
        DemonstrateUpgradableReadOnlyLock();
        Console.WriteLine($"Done {nameof(DemonstrateUpgradableReadOnlyLock)} first time");
        Console.WriteLine();

        Console.WriteLine($"Calling {nameof(DemonstrateUpgradableReadOnlyLock)} second time: ");
        DemonstrateUpgradableReadOnlyLock();
        Console.WriteLine($"Done {nameof(DemonstrateUpgradableReadOnlyLock)} second time");
        Console.WriteLine();

        Console.WriteLine("Demo complete.");
    }

    private static readonly BasicReadWriteVault<BigStruct> BigStructVault =
        new BasicReadWriteVault<BigStruct>(new BigStruct(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Fred"));
}
}