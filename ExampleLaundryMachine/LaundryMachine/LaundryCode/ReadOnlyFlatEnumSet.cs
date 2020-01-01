using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe(true)]
    public readonly struct ReadOnlyFlatEnumSet<[VaultSafeTypeParam] TEnum> : 
        IEquatable<ReadOnlyFlatEnumSet<TEnum>>, IReadOnlyCollection<TEnum> where TEnum : unmanaged, Enum
    {
        public static ref readonly ReadOnlyFlatEnumSet<TEnum> EmptySet => ref TheEmptySet;
        public static ReadOnlyFlatEnumSet<TEnum> AllDefinedEnumValues => new ReadOnlyFlatEnumSet<TEnum>(true);
        public int Count => Arr.Length;
        public TEnum this[int idx] => Arr[idx];
        public bool Contains(TEnum val) => TheCompleteComparer.Contains(Arr, val);
        public int IndexOf(TEnum val) => TheCompleteComparer.BinarySearch(Arr, val);
        
        public ArrayEnumerator<TEnum> GetEnumerator() => new ArrayEnumerator<TEnum>(Arr);
        IEnumerator<TEnum> IEnumerable<TEnum>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public ReadOnlyFlatEnumSet([NotNull] IEnumerable<TEnum> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _arr = TheCompleteComparer.SortAndDeduplicate(source);
        }

        // ReSharper disable once UnusedParameter.Local
        private ReadOnlyFlatEnumSet(bool _) : this(AllDefinedValuesImpl) { }

        private ReadOnlyFlatEnumSet(TEnum[] premadeArray) => _arr = premadeArray;

        static ReadOnlyFlatEnumSet()
        {
            AllTheDefinedEnumValues = new LocklessLazyWriteOnce<TEnum[]>(InitBackingAllDefValArray);
        }

        private static TEnum[] InitBackingAllDefValArray()
        {
            var comparer = TheCompleteComparer;
            IEnumerable<TEnum> allTheValues = Enum.GetValues(typeof(TEnum)).Cast<TEnum>();
            var ret= comparer.SortAndDeduplicate(allTheValues);
            Validate(ret);
            return ret;

        }

        [Conditional("DEBUG")]
        private static void Validate(TEnum[] arr)
        {
            SortedSet<TEnum> set = new SortedSet<TEnum>(arr);
            if (set.Count != arr.Length || !set.SetEquals(arr) || !arr.SequenceEqual(set))
            {
                throw new StateLogicErrorException("The array deduplicating facility does not match that of SortedSet!");
            }
        }

        public override string ToString() => $"[{typeof(ReadOnlyFlatEnumSet<TEnum>).Name}]-- Count: {Count}";
        

        public static bool operator ==(in ReadOnlyFlatEnumSet<TEnum> lhs, in ReadOnlyFlatEnumSet<TEnum> rhs)
        {
            if (lhs.Count == rhs.Count)
            {
                if (lhs.Count == 0) return true;
                for (int i = 0; i < lhs.Count; ++i)
                {
                    if (!TheCompleteComparer.Equals(lhs[i], rhs[i])) return false;
                }

                return true;
            }
            return false;
        }

        public static bool operator !=(in ReadOnlyFlatEnumSet<TEnum> lhs, in ReadOnlyFlatEnumSet<TEnum> rhs) =>
            !(lhs == rhs);

        public override int GetHashCode()
        {
            int hash = Count;
            unchecked
            {
                if (Count > 0)
                {
                    hash = (hash * 397) ^ TheCompleteComparer.GetHashCode(Arr[0]);
                    if (Count > 1)
                        hash = (hash * 397) ^ TheCompleteComparer.GetHashCode(Arr[^1]);
                }
            }
            return hash;
        }

        public override bool Equals(object other) => other is ReadOnlyFlatEnumSet<TEnum> set && set == this;
        public bool Equals(ReadOnlyFlatEnumSet<TEnum> other) => other == this;

        public bool SetEquals(IEnumerable<TEnum> other)
        {
            if (other == null) return false;
            if (other is ReadOnlyFlatEnumSet<TEnum> set)
            {
                return this == set;
            }
            return new ReadOnlyFlatEnumSet<TEnum>(other) == this;
        }


        [NotNull] private static TEnum[] AllDefinedValuesImpl => AllTheDefinedEnumValues;

        private TEnum[] Arr => _arr ?? Array.Empty<TEnum>();
        private readonly TEnum[] _arr;
        private static readonly EnumComparer<TEnum> TheCompleteComparer = new EnumComparer<TEnum>();
        private static readonly ReadOnlyFlatEnumSet<TEnum> TheEmptySet = default;
        [NotNull] private static readonly LocklessLazyWriteOnce<TEnum[]> AllTheDefinedEnumValues;
    }
}