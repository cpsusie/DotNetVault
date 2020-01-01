using System;
using System.Collections.Generic;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    /// <summary>
    ///You could use the stupid reflection based comparer to compare enums or .Equals(obj) and yes it would be fast enough
    /// for many use-cases.... but the fact that you have to box to object or use reflection to compare two generic enum types
    /// that are statically known to be enum types (and by definition backed by one of a very finite set of integral types)
    /// bothers me to the point that I'm willing to use unsafe code to get around and a little pointer ugliness to evade this
    /// stupidity as a matter of general ethics and principles.  If you try to use something other than the actual back of the enum,
    /// you'll get the <see cref="TypeInitializationException"/> and your program will crash long before you can do any invalid
    /// casting.  I use it as a struct because having to pass a comparer as an interface to be accessed indirectly on a potentially large
    /// number of operations in a tight loop (Sort, Bulk set operations, etc) also bothers me as a matter of ethics and principles.
    /// </summary>
    /// <typeparam name="TEnum">the enum type</typeparam>
    /// <typeparam name="TBacker">the enum's backing integral type</typeparam>
    /// <exception cref="TypeInitializationException">This is the only exception throwable by this, which should be thrown
    /// at first use for specified TEnum, TBacker type ... if TBacker is not the integral type backing TEnum, we throw.
    /// After establishing this invariant and due to the nature of the constraints put on the type parameters, all
    /// three public operations are completely safe, but not verifiable.</exception>
    [VaultSafe]
    public readonly struct EnumCompleteComparer<TEnum, TBacker> : IEqualityComparer<TEnum>, IComparer<TEnum> where TEnum : unmanaged, Enum
        where TBacker : unmanaged, IEquatable<TBacker>, IComparable<TBacker>
    {
        public bool Equals(TEnum lhs, TEnum rhs) => //cast to backing type then compare
            EnumCastingUtil<TEnum, TBacker>.CastToBacker(lhs).Equals(EnumCastingUtil<TEnum, TBacker>.CastToBacker(rhs));
        public int GetHashCode(TEnum obj) => EnumCastingUtil<TEnum, TBacker>.CastToBacker(obj).GetHashCode();

        public int Compare(TEnum lhs, TEnum rhs) => EnumCastingUtil<TEnum, TBacker>.CastToBacker(lhs)
            .CompareTo(EnumCastingUtil<TEnum, TBacker>.CastToBacker(rhs));

        public void Sort([NotNull] TEnum[] arr) => EnumCastingUtil<TEnum, TBacker>.Sort(arr);

        public int BinarySearch([NotNull] TEnum[] arr, TEnum val) =>
            EnumCastingUtil<TEnum, TBacker>.BinarySearch(arr, val);

        public TEnum[] SortAndDedupe([NotNull] IEnumerable<TEnum> col) =>
            EnumCastingUtil<TEnum, TBacker>.SortAndDeduplicate(col ?? throw new ArgumentNullException(nameof(col)));

    }
}