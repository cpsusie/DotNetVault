using System;
using JetBrains.Annotations;

namespace VaultUnitTests.ClortonGame
{
    public readonly struct ClortonGameResult
    {
        public readonly DateTime StartedAt;
        public readonly DateTime EndedAt;
        public readonly bool Cancelled;
        [NotNull] public readonly string FinalString;
        public readonly int XCount;
        public readonly int OCount;
        public readonly int? WinningThreadIndex;
        public readonly bool Success;

        internal ClortonGameResult(DateTime start, DateTime end, bool cancel, [NotNull] string final, int xCount,
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