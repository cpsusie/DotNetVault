using System;
using DotNetVault.Logging;
using DotNetVault.TimeStamps;
using JetBrains.Annotations;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Wrapper around DateTime.Now and Hp time stamps.
    /// </summary>
    public abstract class TimeStampProvider
    {
        /// <summary>
        /// Get a timestamp
        /// </summary>
        public abstract DateTime Now { get; }

        /// <summary>
        /// calibrate time stamps -- as supported
        /// </summary>
        public abstract void Calibrate();
    }


    /// <summary>
    /// Used in Clorton Game to get date time.  A high precision stamp provider
    /// used in the test applications and unit tests can be injected.  Otherwise,
    /// just a fancy way to get DateTime.Now.
    /// </summary>
    /// <remarks>Did not want to add the high precision time stamp project
    /// into this library because I want to keep dependencies minimal.</remarks>
    public static class CgTimeStampSource
    {
        /// <summary>
        /// Has any provider (default or ot
        /// </summary>
        public static bool IsProviderSet => TheStampProvider.HasValue;

        /// <summary>
        /// Get a current timestamp
        /// </summary>
        public static DateTime Now => TheStampProvider.Value.Now;

        /// <summary>
        /// Calibrate the time stamp provider, if supported (otherwise do nothing)
        /// </summary>
        public static void Calibrate() => TheStampProvider.Value.Calibrate();

        /// <summary>
        /// Provide an alternate by using this method.  For this to succeed, it must be called before the
        /// first time the <see cref="Now"/> property is accessed.
        /// </summary>
        /// <param name="alternate">the alternate stamp provider</param>
        /// <returns>true for success, false for failure</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SupplyAlternateProvider([NotNull] TimeStampProvider alternate) =>
            TheStampProvider.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));


        private sealed class DateTimeNowWrapper : TimeStampProvider
        {
            internal static TimeStampProvider CreateInstance() => new DateTimeNowWrapper();

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public override DateTime Now => DnvTimeStampProvider.Now;
            
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public override void Calibrate() { }
            
        }

        static CgTimeStampSource() => TheStampProvider = new LocklessWriteOnce<TimeStampProvider>(DateTimeNowWrapper.CreateInstance);
        private static readonly LocklessWriteOnce<TimeStampProvider> TheStampProvider;
    }
}
