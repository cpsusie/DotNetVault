using System;
using DotNetVault.Attributes;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Event arguments for event raised when clorton game ends
    /// </summary>
    [VaultSafe]
    public sealed class ClortonGameFinishedEventArgs : EventArgs
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
        /// CTOR
        /// </summary>
        /// <param name="ts">timestamp</param>
        /// <param name="foundIt">true if thread found it, false otherwise</param>
        /// <param name="idx">reader thread idx</param>
        public ClortonGameFinishedEventArgs(DateTime ts, bool foundIt, int idx)
        {
            TimeStamp = ts;
            FoundIt = foundIt;
            ThreadIdx = idx;
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"At [{TimeStamp:O}] thread number {ThreadIdx.ToString()} terminated. " + (FoundIt
                ? "It found it."
                : "It did not find it");
    }
}