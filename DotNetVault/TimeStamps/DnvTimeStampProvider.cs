using System;
using System.Runtime.CompilerServices;
using HpTimeStamps;
using MonotonicContext = HpTimeStamps.MonotonicStampContext;
using WallClock = System.DateTime;
using HpStamp = System.DateTime;
using HpStampSource = HpTimeStamps.TimeStampSource;



namespace DotNetVault.TimeStamps
{
    using MonotonicStamp = MonotonicTimeStamp<MonotonicContext>;
    using MonoStampSource = MonotonicTimeStampUtil<MonotonicContext>;

    internal static class DnvTimeStampProvider
    {
        /// <summary>
        /// Indicates type of stamps retrieved by <see cref="Now"/> and <see cref="UtcNow"/>
        /// properties.
        /// </summary>
        public static DefaultStampType DefaultStampType => DefaultStampType.Monotonic;
        

        /// <summary>
        /// Use the default provider to get a timestamp expressed in local time
        /// </summary>
        public static DateTime Now
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MonoLocalNow;
        }

        /// <summary>
        /// Use the default provider to get a timestamp in UTC time
        /// </summary>
        public static DateTime UtcNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MonoUtcNow;
        }

        /// <summary>
        /// Get a monotonic timestamp
        /// </summary>
        public static MonotonicStamp MonoNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MonoStampSource.StampNow;
        }

        /// <summary>
        /// Get a monotonic timestamp expressed as a utc datetime
        /// </summary>
        public static DateTime MonoUtcNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MonoNow.ToUtcDateTime();
        }

        /// <summary>
        /// Get a monotonic timestamp expressed as a local datetime
        /// </summary>
        public static DateTime MonoLocalNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MonoNow.ToLocalDateTime();
        }

        /// <summary>
        /// Get a high precision timestamp expressed as a local time
        /// </summary>
        public static HpStamp HpNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => HpStampSource.Now;
        }

        /// <summary>
        /// Get a high precision timestamp expressed in UTC
        /// </summary>
        public static DateTime HpUtcNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => HpStampSource.UtcNow;
        }

        /// <summary>
        /// Get the wall clock time expressed as local time
        /// </summary>
        public static DateTime WallNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => WallClock.Now;
        }

        /// <summary>
        /// Get the wall clock time expressed in utc
        /// </summary>
        public static HpStamp WallUtcNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => WallClock.UtcNow;
        }

        /// <summary>
        /// True if, on this thread, the high precision clock needs calibration.
        /// </summary>
        public static bool HpNeedsCalibration
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => HpStampSource.NeedsCalibration;
        }

        /// <summary>
        /// The amount of time elapsed since the High Precision clock's
        /// last calibration on this thread.
        /// </summary>
        public static TimeSpan TimeSinceLastCalibration
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => HpStampSource.TimeSinceCalibration;
        }

        /// <summary>
        /// Monotonic stamp context used by monotonic clock.  Contains
        /// information about frequencies, conversions, a reference time, etc.
        /// </summary>
        public static ref readonly MonotonicContext MonotonicContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref MonoStampSource.StampContext;
        }

        #region Mutators
        /// <summary>
        /// Calibrate the high precision clock now
        /// </summary>
        public static void CalibrateNow() => HpStampSource.Calibrate();
        #endregion
    }
}
