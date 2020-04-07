using System;
using JetBrains.Annotations;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Helps with output
    /// </summary>
    public interface IOutputHelper 
    {
        /// <summary>
        /// Adds a line of text to the output.
        /// </summary>
        /// <param name="message"></param>
        void WriteLine(string message);


        /// <summary>
        /// Formats a line of text and adds it to the output.
        /// </summary>
        /// <param name="format">the message format</param>
        /// <param name="args">the format arguments</param>
        void WriteLine(string format, params object[] args);
    }

    /// <summary>
    /// If the output helper is implemented as a buffer, you can retrieve and retrieve and
    /// clear the buffer using an implementation of this interface.
    /// </summary>
    public interface IBufferBasedOutputHelper : IDisposableOutputHelper
    {
        /// <summary>
        /// Get the current contents of the buffer
        /// </summary>
        /// <param name="timeout">how long to wait to get access to the buffer.</param>
        /// <returns>the buffer text</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was not positive.</exception>
        /// <exception cref="ObjectDisposedException">the object has been disposed</exception>
        /// <exception cref="TimeoutException">Unable to obtain access to the buffer within the time specified by <paramref name="timeout"/>.</exception>
        [NotNull] string GetCurrentText(TimeSpan timeout);

        /// <summary>
        /// Get the current content of the buffer THEN CLEAR the buffer
        /// </summary>
        /// <param name="timeout">how long the wait to get access to the buffer.</param>
        /// <returns>the contents of the buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> was not positive.</exception>
        /// <exception cref="ObjectDisposedException">the object has been disposed</exception>
        /// <exception cref="TimeoutException">Unable to obtain access to the buffer within the time specified by <paramref name="timeout"/>.</exception>
        [NotNull] string GetCurrentTextAndClearBuffer(TimeSpan timeout);
    }

    /// <summary>
    /// A output helper that raises an event whenever a line is appended can implement this interface
    /// </summary>
    public interface IEventRaisingOutputHelper : IDisposableOutputHelper
    {
        /// <summary>
        /// Raised whenever text is appended.  Includes timestamp and appended text.
        /// </summary>
        event EventHandler<OutputHelperAppendedToEventArgs> TextAppended;
    }
    
    /// <summary>
    /// output helpers implementing this interface need to be and
    /// should be disposed.
    /// </summary>
    public interface IDisposableOutputHelper : IOutputHelper, IDisposable
    {
        /// <summary>
        /// True if disposed; false otherwise.
        /// </summary>
        bool IsDisposed { get; }
    }
}