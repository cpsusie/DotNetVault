using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.RefReturningCollections;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// A limited purpose unsigned equatable and comparable 256 integer used to demonstrate
    /// value list vaults.
    /// </summary>
    [DataContract]
    public readonly struct UInt256 : IEquatable<UInt256>, IComparable<UInt256>
    {

        /// <summary>
        /// Create instance
        /// </summary>
        /// <param name="high">high 64 bits</param>
        /// <param name="midHigh">next 64 bits</param>
        /// <param name="midLow">next 64 bits </param>
        /// <param name="low">low 64 bits</param>
        public UInt256(ulong high, ulong midHigh, ulong midLow, ulong low)
        {
            _high = high;
            _middleHigh = midHigh;
            _middleLow = midLow;
            _low = low;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(81);
            sb.Append("0x");
            string str = _high.ToString("X16")  + 
                         _middleHigh.ToString("X16") + 
                         _middleLow.ToString("X16" ) + _low.ToString("X16");
            int count = 0;
            foreach (char c in str)
            {
                sb.Append(c);
                if (++count % 4 == 0 && count < 64)
                {
                    sb.Append("_");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// test two values for equality
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if equal false otherwise</returns>
        public static bool operator ==(in UInt256 lhs, in UInt256 rhs) =>
            lhs._high == rhs._high && lhs._middleHigh == rhs._middleHigh && lhs._middleLow == rhs._middleLow &&
            lhs._low == rhs._low;
        /// <summary>
        /// test two values for inequality
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if not equal false if equal++ otherwise</returns>
        public static bool operator !=(in UInt256 lhs, in UInt256 rhs) => !(lhs == rhs);
        /// <summary>
        /// Test two values to see if left is greater than right
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if the left hand operand is greater than the right hand operand, false otherwise</returns>
        public static bool operator >(in UInt256 lhs, in UInt256 rhs) => Compare(in lhs, in rhs) > 0;
        /// <summary>
        /// Test two values to see if left is less than right
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if the left hand operand is less than the right hand operand, false otherwise</returns>
        public static bool operator <(in UInt256 lhs, in UInt256 rhs) => Compare(in lhs, in rhs) < 0;
        /// <summary>
        /// Test two values to see if left is greater than or equal to right
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if the left hand operand is greater than or equal to the right hand operand, false otherwise</returns>
        public static bool operator >=(in UInt256 lhs, in UInt256 rhs) => !(lhs < rhs);
        /// <summary>
        /// Test two values to see if left is less than or equal to right
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if the left hand operand is less than or equal to the right hand operand, false otherwise</returns>
        public static bool operator <=(in UInt256 lhs, in UInt256 rhs) => !(lhs > rhs);
        
        /// <inheritdoc />
        public bool Equals(UInt256 other) => this == other;

        /// <inheritdoc />
        public override bool Equals(object other) => other is UInt256 o && o == this;

        /// <inheritdoc />
        public int CompareTo(UInt256 other) => Compare(in this, in other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = _low.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ _middleLow.GetHashCode();
                hash = (hash * 397) ^ _middleHigh.GetHashCode();
                hash = (hash * 397) ^ _high.GetHashCode();
            }
            return hash;
        }


        /// <summary>
        /// Compare two values
        /// </summary>
        /// <param name="lhs">left hand value</param>
        /// <param name="rhs">right hand value</param>
        /// <returns>negative number if left less than right, 0 if equal, positive if left greater than right</returns>
        public static int Compare(in UInt256 lhs, in UInt256 rhs)
        {
            int ret;

            int highComparison = lhs._high.CompareTo(rhs._high);
            if (highComparison == 0)
            {
                int midHighComp = lhs._middleHigh.CompareTo(rhs._middleHigh);
                if (midHighComp == 0)
                {
                    int midLowComp = lhs._middleLow.CompareTo(rhs._middleLow);
                    ret = midLowComp == 0 ? lhs._low.CompareTo(rhs._low) : midLowComp;
                }
                else
                {
                    ret = midHighComp;
                }
            }
            else
            {
                ret = highComparison;
            }

            return ret;
        }

        [DataMember] private readonly ulong _high;
        [DataMember] private readonly ulong _middleHigh;
        [DataMember] private readonly ulong _middleLow;
        [DataMember] private readonly ulong _low;
    }

    /// <summary>
    /// Example of template use: TimeSpan
    /// </summary>
    [VaultSafe]
    public readonly struct UInt256CompleteComparer : IByRefCompleteComparer<UInt256>
    {
        /// <summary>
        /// True if this type works correctly when default constructed.
        /// </summary>
        public bool WorksCorrectlyWhenDefaultConstructed => true;
        /// <summary>
        /// True if this type works correctly when default constructed.
        /// </summary>
        public bool IsValid => true;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(in UInt256 lhs, in UInt256 rhs) => lhs == rhs;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(in UInt256 obj) => obj.GetHashCode();
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(in UInt256 lhs, in UInt256 rhs) => UInt256.Compare(in lhs, in rhs);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UInt256 x, UInt256 y) => Equals(in x, in y);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(UInt256 obj) => GetHashCode(in obj);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(UInt256 x, UInt256 y) => Compare(in x, in y);
    }

    /// <summary>
    /// A delegate used to establish whether a relation between lhs
    /// and rhs exists.  Accepts by reference.
    /// </summary>
    /// <typeparam name="T">The type</typeparam>
    /// <param name="lhs">left hand comparand</param>
    /// <param name="rhs">right hand comparand</param>
    /// <returns>true if the relation exists, false otherwise</returns>
    public delegate bool RefRelationPredicate<T>(in T lhs, in T rhs);

    /// <summary>
    /// An array wrapper that can be data contract serialized
    /// and allows by reference indexing and enumeration.
    /// </summary>
    /// <typeparam name="T">the type stored</typeparam>
    [DataContract]
    [VaultSafe(true)]
    public readonly struct ReadOnlyArrayWrapper<[VaultSafeTypeParam] T> : IEquatable<ReadOnlyArrayWrapper<T>>, IReadOnlyList<T>
    {
        /// <summary>
        /// Convert an immutable array to this wrapper
        /// </summary>
        /// <param name="convert">the immutable array</param>
        public static implicit operator ReadOnlyArrayWrapper<T>(ImmutableArray<T> convert) =>
            new ReadOnlyArrayWrapper<T>(convert.ToArray());

        /// <summary>
        /// Convert one of these guys to an immutable array
        /// </summary>
        /// <param name="wrapper">to be converted</param>
        public static implicit operator ImmutableArray<T>(ReadOnlyArrayWrapper<T> wrapper) =>
            wrapper._arr.ToImmutableArray();

        /// <summary>
        /// Create a readonly array from a collection
        /// </summary>
        /// <param name="source">the collection</param>
        /// <returns>a readonly array</returns>
        public static ReadOnlyArrayWrapper<T> CreateReadonlyArray([NotNull] IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new ReadOnlyArrayWrapper<T>(source.ToArray());
        }

        /// <summary>
        /// Uninitialized value
        /// </summary>
        public static ReadOnlyArrayWrapper<T> Default { get; } = default;

        /// <summary>
        /// Initialized but empty value
        /// </summary>
        public static ReadOnlyArrayWrapper<T> Empty { get; } = new ReadOnlyArrayWrapper<T>(Array.Empty<T>());

        /// <summary>
        /// True if the value has not been initialized.
        /// </summary>
        public bool IsDefault => _arr == null;

        /// <summary>
        /// Get the item at the specified index
        /// </summary>
        /// <param name="index">the index to get</param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <exception cref="NullReferenceException">Has default value.</exception>
        public ref readonly T this[int index] => ref _arr[index];
        /// <inheritdoc />
        public int Count => _arr.Length;

        /// <summary>
        /// Alias for <see cref="Count"/>
        /// </summary>
        public int Length => _arr.Length;
        
        /// <summary>
        /// Provides array's count as a long.
        /// </summary>
        public long LongLength => _arr.LongLength;

        /// <summary>
        /// Convert to a read-only span.
        /// </summary>
        /// <returns>A read-only span as a view of the array</returns>
        public ReadOnlySpan<T> AsSpan() => _arr.AsSpan();

        /// <summary>
        /// see if item occurs in collection
        /// </summary>
        /// <param name="item">item </param>
        /// <param name="eqComparer">equality comparer</param>
        /// <returns>true if it appears in collection, false otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="eqComparer"/> was null</exception>
        public bool Contains(in T item, [NotNull] RefRelationPredicate<T> eqComparer)
            => IndexOf(in item, eqComparer) > -1;

        /// <summary>
        /// see at which index the item first appears in collection.
        /// </summary>
        /// <param name="item">the item</param>
        /// <param name="eqComparer">equality comparer</param>
        /// <returns>the index if found, -1 otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="eqComparer"/> was null.</exception>
        public int IndexOf(in T item, [NotNull] RefRelationPredicate<T> eqComparer )
        {
            if (eqComparer == null) throw new ArgumentNullException(nameof(eqComparer));
            for (int i = 0; i < _arr.Length; ++i)
            {
                if (eqComparer(in item, in _arr[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        T IReadOnlyList<T>.this[int index] => this[index];

        /// <summary>
        /// Get an enumerator
        /// </summary>
        /// <returns>an enumerator</returns>
        public Enumerator GetEnumerator() => Enumerator.GetEnumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        /// <summary>
        /// Compares two objects of this type to see if they wrap (reference equality only)
        /// the same underlying array
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if they wrap same array, false otherwise</returns>
        public static bool operator ==(ReadOnlyArrayWrapper<T> lhs, ReadOnlyArrayWrapper<T> rhs) =>
            ReferenceEquals(lhs._arr, rhs._arr);
        /// <summary>
        /// Compares two objects of this type to see if they wrap (reference equality only)
        /// distinct underling arrays 
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if they wrap distinct arrays, false otherwise</returns>
        public static bool operator !=(ReadOnlyArrayWrapper<T> lhs, ReadOnlyArrayWrapper<T> rhs) => !(lhs == rhs);

        /// <inheritdoc />
        public override int GetHashCode() => _arr?.GetHashCode() ?? 0;

        /// <inheritdoc />
        public override bool Equals(object other) => other is ReadOnlyArrayWrapper<T> roaw && roaw == this;

        /// <inheritdoc />
        public bool Equals(ReadOnlyArrayWrapper<T> other) => other == this;

        private ReadOnlyArrayWrapper([NotNull] T[] arr) => _arr = arr ?? throw new ArgumentNullException(nameof(arr));

        /// <summary>
        /// A by ref enumerator
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            /// <summary>
            /// Create enumerator
            /// </summary>
            /// <param name="owner">the owner</param>
            /// <returns>an enumerator</returns>
            internal static Enumerator GetEnumerator(ReadOnlyArrayWrapper<T> owner) => new Enumerator(owner);


            /// <summary>
            /// Get the current item
            /// </summary>
            /// <exception cref="NullReferenceException">Enumerator has not been initialized.</exception>
            /// <exception cref="IndexOutOfRangeException">Enumerator not in valid state.</exception>
            public readonly ref readonly T Current => ref _arr[_idx];

            T IEnumerator<T>.Current
            {
                get
                {
                    if (_arr == null) throw new InvalidOperationException("Enumerator has not been initialized.");
                    return Current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_arr == null) throw new InvalidOperationException("Enumerator has not been initialized.");
                    return Current;
                }
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_arr == null)
                {
                    throw new InvalidOperationException("The enumerator has not been initialized.");
                }

                int temp = ++_idx;
                return temp > -1 && temp < _arr.Length;
            }

            /// <inheritdoc />
            public void Reset()
            {
                if (_arr == null)
                {
                    throw new InvalidOperationException("The enumerator has not been initialized.");
                }

                _idx = -1;
            }

            /// <inheritdoc />
            public void Dispose() {}

            private Enumerator(ReadOnlyArrayWrapper<T> wrapper)
            {
                _arr = wrapper._arr ??
                       throw new ArgumentException(
                           @"The array has not been initialized.", nameof(wrapper));
                _idx = -1;
            }

            private int _idx;
            private readonly T[] _arr;
        }

        [DataMember] private readonly T[] _arr;
        

    }

}