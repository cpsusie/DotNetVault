using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Represents the result of a Clorton Game
    /// </summary>
    [VaultSafe]
    public readonly struct ClortonGameResult : IEquatable<ClortonGameResult>
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
        /// The final value of the string 
        /// </summary>
        [NotNull] public readonly string FinalString;
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

        /// <inheritdoc />
        public bool Equals(ClortonGameResult other) => this == other;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ClortonGameResult other && other == this;

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
        public static bool operator ==(in ClortonGameResult left, in ClortonGameResult right) =>
            left.StartedAt == right.StartedAt && left.EndedAt == right.EndedAt && left.Cancelled == right.Cancelled &&
            left.FinalString == right.FinalString && left.XCount == right.XCount && left.OCount == right.OCount &&
            left.WinningThreadIndex == right.WinningThreadIndex && left.Success == right.Success;

        /// <summary>
        /// Compare two values for equality
        /// </summary>
        /// <param name="left">the left hand operand</param>
        /// <param name="right">the right hand operand</param>
        /// <returns>True if values are NOT the same, false otherwise </returns>
        public static bool operator !=(in ClortonGameResult left, in ClortonGameResult right) => 
            !(left == right);


        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="start">start time</param>
        /// <param name="end">end time</param>
        /// <param name="cancel">was canceled?</param>
        /// <param name="final">final string val</param>
        /// <param name="xCount">final x count</param>
        /// <param name="oCount">final o count</param>
        /// <param name="winningThreadIdx">winning index of reader thread, if any  (null if not)</param>
        /// <exception cref="ArgumentNullException"><paramref name="final"/> was null.</exception>
        public ClortonGameResult(DateTime start, DateTime end, bool cancel, [NotNull] string final, int xCount,
            int oCount, int? winningThreadIdx)
        {
            StartedAt = start;
            EndedAt = end;
            Cancelled = cancel;
            FinalString = final ?? throw new ArgumentNullException(nameof(final));
            XCount = xCount;
            OCount = oCount;
            WinningThreadIndex = winningThreadIdx;
            Success = winningThreadIdx != null;
        }
    }
}