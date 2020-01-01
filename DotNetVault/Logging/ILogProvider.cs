using System;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    /// <summary>
    /// An interface for a type that provides logging services
    /// </summary>
    public interface ILogProvider : IDisposable
    {
        /// <summary>
        /// Logger is disposed if true
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Logger is running if true
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Log the specified text
        /// </summary>
        /// <param name="text">the text to log</param>
        void Log(string text);

        /// <summary>
        /// Log the specified exception
        /// </summary>
        /// <param name="ex">exception was null</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> was null</exception>
        void Log([NotNull] Exception ex);
    }
}
