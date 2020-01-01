using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;
#pragma warning disable 1591 //Strictly for unit tests but needs to be public for some reason

namespace DotNetVault.Miscellaneous
{
    [VaultSafe]
    public sealed class AnimalName : IEquatable<AnimalName>, IComparable<AnimalName>
    {
        public Guid UniqueIdentifier { get; }

        public string Name { get; }


        public AnimalName([NotNull] string nameText) :
            this(nameText, Guid.NewGuid())
        { }

        private AnimalName([NotNull] string textName, Guid identifier)
        {
            if (textName == null) throw new ArgumentNullException(nameof(textName));
            if (string.IsNullOrWhiteSpace(textName)) throw new ArgumentException(
                @"Animals deserve non-empty names that are not just whitespace.", nameof(textName));

            Name = textName;
            UniqueIdentifier = identifier;
        }

        public override string ToString() => Name;


        [Pure]
        public AnimalName ChangeTextName(string newTextName) => new AnimalName(newTextName, UniqueIdentifier);
        public bool Equals(AnimalName other)
            => UniqueIdentifier == other?.UniqueIdentifier && AnimalNameTextComparerSource.AnimalNameTextComparer.Equals(Name, other.Name);
        public override int GetHashCode() => UniqueIdentifier.GetHashCode();
        public override bool Equals(object obj) => Equals(obj as AnimalName);
        public static bool operator !=(AnimalName lhs, AnimalName rhs) => !(lhs == rhs);
        public static bool operator >(AnimalName lhs, AnimalName rhs) => Compare(lhs, rhs) > 0;
        public static bool operator <(AnimalName lhs, AnimalName rhs) => Compare(lhs, rhs) < 0;
        public static bool operator >=(AnimalName lhs, AnimalName rhs) => !(lhs < rhs);
        public static bool operator <=(AnimalName lhs, AnimalName rhs) => !(lhs > rhs);

        public int CompareTo(AnimalName other)
        {
            if (other == null) return 1;

            int textComparison = AnimalNameTextComparerSource.AnimalNameTextComparer.Compare(Name, other.Name);
            return textComparison == 0 ? UniqueIdentifier.CompareTo(other.UniqueIdentifier) : textComparison;
        }

        public static bool operator ==(AnimalName lhs, AnimalName rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null)) return false;
            return lhs.Equals(rhs);
        }

        private static int Compare(AnimalName lhs, AnimalName rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return 0;
            if (ReferenceEquals(lhs, null)) return -1;
            if (ReferenceEquals(rhs, null)) return 1;
            return lhs.CompareTo(rhs);
        }


    }
}