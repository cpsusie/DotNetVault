using System;
using System.Runtime.CompilerServices;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.RefReturningCollections;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// A limited purpose unsigned equatable and comparable 256 integer used to demonstrate
    /// value list vaults.
    /// </summary>
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

        private readonly ulong _high;
        private readonly ulong _middleHigh;
        private readonly ulong _middleLow;
        private readonly ulong _low;
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

}