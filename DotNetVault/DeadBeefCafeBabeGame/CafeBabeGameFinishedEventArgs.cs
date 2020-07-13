using System;
using DotNetVault.Attributes;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Event arguments for event raised when clorton game ends
    /// </summary>
    [VaultSafe]
    public sealed class CafeBabeGameFinishedEventArgs : EventArgs
    {
        /// <summary>
        /// Time of event
        /// </summary>
        public DateTime TimeStamp { get; }
        /// <summary>
        /// True if found, false otherwise
        /// </summary>
        public bool FoundIt { get; }
        /// <summary>
        /// Index of the thread that finished
        /// </summary>
        public int ThreadIdx { get; }

        /// <summary>
        /// If this thread found the number it sought in the list, contains that number's index.
        /// Otherwise, null.
        /// </summary>
        public int? LocatedIndex { get; }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="ts">timestamp</param>
        /// <param name="foundIt">true if thread found it, false otherwise</param>
        /// <param name="threadIdx">reader thread idx</param>
        /// <param name="identifiedIdx">if it found the sought after number in the list, the index in the list where it was found.</param>
        public CafeBabeGameFinishedEventArgs(DateTime ts, bool foundIt, int threadIdx, int? identifiedIdx)
        {
            TimeStamp = ts;
            FoundIt = foundIt;
            ThreadIdx = threadIdx;
            LocatedIndex = identifiedIdx;
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"At [{TimeStamp:O}] thread number {ThreadIdx.ToString()} terminated. " + (FoundIt
                ? "It found it."
                : "It did not find it");
    }
}
