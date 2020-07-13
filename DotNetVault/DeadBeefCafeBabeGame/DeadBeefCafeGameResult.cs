using System;
using System.Collections.Immutable;
using DotNetVault.Attributes;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Represents the result of a Clorton Game
    /// </summary>
    [VaultSafe]
    public readonly struct DeadBeefCafeGameResult : IEquatable<DeadBeefCafeGameResult>
    {
        /// <summary>
        /// Time it started
        /// </summary>
        public readonly DateTime StartedAt;
        /// <summary>
        /// Time it ended
        /// </summary>
        public readonly DateTime EndedAt;
        /// <summary>
        /// Whether the game ended bc cancelled
        /// </summary>
        public readonly bool Cancelled;
        /// <summary>
        /// The final value of the array 
        /// </summary>
        public readonly ImmutableArray<UInt256> FinalArray;
        /// <summary>
        /// Final 'x' count in string (may not be value when clorton was written)
        /// </summary>
        public readonly int XCount;
        /// <summary>
        /// Final 'o' count in string (may not be value when clorton was written)
        /// </summary>
        public readonly int OCount;
        /// <summary>
        /// The index of the winning reader thread (null if game ended without winner)
        /// </summary>
        public readonly int? WinningThreadIndex;
        /// <summary>
        /// The game ended successfully.
        /// </summary>
        public readonly bool Success;

        /// <summary>
        /// The index at which <see cref="DeadBeefCafeBabeGameConstants.LookForNumber"/> was written, if any.
        /// </summary>
        public readonly int? NumberFoundAtIndex;

        /// <inheritdoc />
        public bool Equals(DeadBeefCafeGameResult other) => this == other;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DeadBeefCafeGameResult other && other == this;

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StartedAt.GetHashCode();
                hashCode = (hashCode * 397) ^ EndedAt.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Compare two values for equality
        /// </summary>
        /// <param name="left">the left hand operand</param>
        /// <param name="right">the right hand operand</param>
        /// <returns>True if values are the same, false otherwise </returns>
        public static bool operator ==(in DeadBeefCafeGameResult left, in DeadBeefCafeGameResult right) =>
            left.StartedAt == right.StartedAt && left.EndedAt == right.EndedAt && left.Cancelled == right.Cancelled &&
            Equals(left.FinalArray, right.FinalArray) && left.XCount == right.XCount && left.OCount == right.OCount &&
            left.WinningThreadIndex == right.WinningThreadIndex && left.Success == right.Success &&
            left.NumberFoundAtIndex == right.NumberFoundAtIndex;

        /// <summary>
        /// Compare two values for equality
        /// </summary>
        /// <param name="left">the left hand operand</param>
        /// <param name="right">the right hand operand</param>
        /// <returns>True if values are NOT the same, false otherwise </returns>
        public static bool operator !=(in DeadBeefCafeGameResult left, in DeadBeefCafeGameResult right) =>
            !(left == right);


        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="start">start time</param>
        /// <param name="end">end time</param>
        /// <param name="cancel">was canceled?</param>
        /// <param name="final">final array val</param>
        /// <param name="xCount">final x count</param>
        /// <param name="oCount">final o count</param>
        /// <param name="lookForNumberFoundAtIdx">the index of <see cref="DeadBeefCafeBabeGameConstants.LookForNumber"/> in the array,
        /// if found.</param>
        /// <param name="winningThreadIdx">winning index of reader thread, if any  (null if not)</param>
        /// <exception cref="ArgumentException"><paramref name="final"/> was not initialized.</exception>
        public DeadBeefCafeGameResult(DateTime start, DateTime end, bool cancel,  ImmutableArray<UInt256> final, int xCount,
            int oCount, int? lookForNumberFoundAtIdx, int? winningThreadIdx)
        {
            StartedAt = start;
            EndedAt = end;
            Cancelled = cancel;
            FinalArray = final.IsDefault ? throw new ArgumentException(@"The parameter was not initialized", nameof(final)) : final;
            XCount = xCount;
            OCount = oCount;
            WinningThreadIndex = winningThreadIdx;
            Success = winningThreadIdx != null;
            NumberFoundAtIndex = lookForNumberFoundAtIdx;
        }

        private static bool Equals(ImmutableArray<UInt256> lhs, ImmutableArray<UInt256> rhs)
        {
            if (lhs.IsDefault && rhs.IsDefault) return true;
            if (lhs.IsDefault || rhs.IsDefault) return false;
            if (lhs.Length != rhs.Length) return false;
            
            for (int i = 0; i < lhs.Length; ++i)
            {
                ref readonly var lVal = ref lhs.ItemRef(i);
                ref readonly var rVal = ref rhs.ItemRef(i);
                if (lVal != rVal) return false;
            }
            return true;
        }


    }
}