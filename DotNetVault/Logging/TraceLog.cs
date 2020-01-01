using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    /// <summary>
    /// A class that writes trace logs information to the trace log 
    /// </summary>
    internal static class TraceLog
    {
        /// <summary>
        /// Create a logger that logs stuff to the specified file as a trace log.
        /// Active by default in in debug and release mode
        /// </summary>
        /// <param name="fileName">the file name that should be written to</param>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> was null</exception>
        /// <exception cref="InvalidOperationException">The logger has already been supplied for this process.</exception>
        public static void CreateLogger([NotNull] string fileName)
        {
            LoggerImpl.SupplyLogger(LogProvider.CreateInstance(fileName));
        }

        /// <summary>
        /// Writes the string to the log
        /// </summary>
        /// <param name="message">the string to log</param>
        [Conditional("TRACE")]
        public static void Log(string message) => LoggerImpl.Provider.Log(message);

        /// <summary>
        /// Writes the exception to the log
        /// </summary>
        /// <param name="ex">the exception to log</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> was null.</exception>
        [Conditional("TRACE")]
        public static void Log([NotNull] Exception ex) =>
            LoggerImpl.Provider.Log(ex ?? throw new ArgumentNullException(nameof(ex)));

    }
}