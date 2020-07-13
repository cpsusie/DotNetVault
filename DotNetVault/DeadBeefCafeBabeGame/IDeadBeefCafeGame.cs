using System;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// An interface that varied versions of the dead beef cafe babe game implement
    /// </summary>
    public interface IDeadBeefCafeGame : IDisposable, IDeadBeefCafeBabeGameConstants
    {
        /// <summary>
        /// Raised when the game ends
        /// </summary>
        event EventHandler<DeadBeefCafeGameEndedEventArgs> GameEnded;
        /// <summary>
        /// True if the game has been disposed
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// True if start has ever been requested
        /// </summary>
        bool StartEverRequested { get; }
        /// <summary>
        /// True if the game ever started
        /// </summary>
        bool EverStarted { get; }
        /// <summary>
        /// True if the game was cancelled
        /// </summary>
        bool IsCancelled { get; }
        /// <summary>
        /// The number of pending reader threads
        /// </summary>
        int PendingReaderThreads { get; }
    }
}
