using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    /// <summary>
    /// A class that writes debug log information to the debug log
    ///
    /// All logging methods are conditionally compiled ... only present if
    /// "DEBUG" constant is true
    /// </summary>
    public static class DebugLog
    {
        /// <summary>
        /// Create a logger that logs stuff to the specified file as a debug log.
        /// Active only in debug mode
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
        [Conditional("DEBUG")]
        public static void Log(string message) => Log(message, false);

        /// <summary>
        /// Writes the string to the log
        /// </summary>
        /// <param name="message">the string to log</param>
        /// <param name="breakIfAttached">whether a debugger, if attached, should break here</param>
        [Conditional("DEBUG")]
        public static void Log(string message, bool breakIfAttached)
        {
            LoggerImpl.Provider.Log(message);
            Debug.WriteLine(message);
            if (breakIfAttached && Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }


        /// <summary>
        /// Writes the exception to the log
        /// </summary>
        /// <param name="ex">the exception to log</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> was null.</exception>
        [Conditional("DEBUG")]
        public static void Log([NotNull] Exception ex) => Log(ex, false);


        /// <summary>
        /// Writes the exception to the log
        /// </summary>
        /// <param name="ex">the exception to log</param>
        /// <param name="breakIfAttached">whether an attached debugger should break here</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> was null.</exception>
        [Conditional("DEBUG")]
        public static void Log([NotNull] Exception ex, bool breakIfAttached)
        {
            LoggerImpl.Provider.Log(ex ?? throw new ArgumentNullException(nameof(ex)));
            Debug.WriteLine(ex);
            if (breakIfAttached && Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
    }
}