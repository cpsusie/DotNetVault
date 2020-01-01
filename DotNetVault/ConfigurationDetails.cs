
namespace DotNetVault
{
    /// <summary>
    /// Used to get info on whether a debug, trace, etc build
    /// </summary>
    public static class ConfigurationDetails
    {
#if DEBUG
        /// <summary>
        /// Signifies whether this binary is a debug-enabled binary
        /// </summary>
        public const bool IsDebugBuild = true;
#else
        /// <summary>
        /// Signifies whether this binary is a debug-enabled binary
        /// </summary>
        public const bool IsDebugBuild = false;
#endif
#if TRACE
        /// <summary>
        /// Signifies whether this build is a trace-enabled binary
        /// </summary>
        public const bool IsTraceBuild = true;
#else
        /// <summary>
        /// Signifies whether this build is a trace-enabled binary
        /// </summary>
        public const bool IsTraceBuild = false;
#endif
        /// <summary>
        /// Signifies whether this build is a release build
        /// </summary>
        public const bool IsReleaseBuild = !IsDebugBuild;

    }
}
