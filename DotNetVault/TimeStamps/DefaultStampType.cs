using System;

namespace DotNetVault.TimeStamps
{
    /// <summary>
    /// Indicates which clock <see cref="DnvTimeStampProvider.Now"/> and <see cref="DnvTimeStampProvider.UtcNow"/>
    /// use.
    /// </summary>
    internal enum DefaultStampType
    {
        /// <summary>
        /// Default stamps are values of <see cref="DateTime"/> retrieved
        /// from a monotonic source.
        /// </summary>
        Monotonic = 0,
        /// <summary>
        /// Default stamps are values of <see cref="DateTime"/> retrieved
        /// from a High Precision (per-thread calibrated, periodically recalibrating,
        /// non-monotonic source).
        /// </summary>
        HighPrecision,
        /// <summary>
        /// Default stamps are values of <see cref="DateTime"/> that reflect the
        /// Wall clock: not monotonic, not high precision, subject to change
        /// based on timezone, synchronization with time server, daylights savings time,
        /// etc.
        /// </summary>
        Wall
    }
}