using System;

namespace ExampleCodePlayground
{
    public readonly struct UInt128 : IEquatable<UInt128>, IComparable<UInt128>
    {
        public static ref readonly UInt128 Zero => ref TheZeroVal;
        public static ref readonly UInt128 MinValue => ref Zero;
        public static ref readonly UInt128 MaxValue => ref TheMaxValue;
    
        public static int Compare(in UInt128 lhs, in UInt128 rhs)
        {
            int highCompare = lhs._high.CompareTo(rhs._high);
            return highCompare == 0 ? lhs._low.CompareTo(rhs._low) : highCompare;
        }

        public UInt128(ulong high, ulong low)
        {
            _low = low;
            _high = high;
        }

        public static bool operator ==(in UInt128 lhs, in UInt128 rhs) => lhs._high == rhs._high && lhs._low == rhs._low;
        public static bool operator !=(in UInt128 lhs, in UInt128 rhs) => !(lhs==rhs);
        public static bool operator >(in UInt128 lhs, in UInt128 rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in UInt128 lhs, in UInt128 rhs) => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in UInt128 lhs, in UInt128 rhs) => !(lhs < rhs);
        public static bool operator <=(in UInt128 lhs, in UInt128 rhs) => !(lhs > rhs);
        public bool Equals(UInt128 other) => other == this;
        public int CompareTo(UInt128 other) => Compare(in this, in other);
        public override bool Equals(object other) => other is UInt128 u128 && u128 == this;
        public override string ToString() => "0x" + _high.ToString("X8") + _low.ToString("X8");
    
        public override int GetHashCode()
        {
            int hash = _high.GetHashCode();
            unchecked
            {
                return ((hash * 397) ^ _low.GetHashCode());
            }
        }

        private readonly ulong _high;
        private readonly ulong _low;
        private static readonly UInt128 TheZeroVal = default;
        private static readonly UInt128 TheMaxValue = new UInt128(ulong.MaxValue, ulong.MaxValue);
    
    }
}