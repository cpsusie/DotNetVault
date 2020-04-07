using System;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Constants used by clorton game.
    /// </summary>
    public interface IClortonGameConstants
    {
        /// <summary>
        /// The text the arbiter thread writes on the termination condition detection
        /// and the reader threads seek this text
        /// </summary>
        string LookForText { get; }

        /// <summary>
        /// The char written by the x writer thread
        /// </summary>
        char XChar { get; }

        /// <summary>
        /// The char written by the x writer thread
        /// </summary>
        char OChar { get; }
    }

    /// <summary>
    /// Implementation of constants
    /// </summary>
    public readonly struct ClortonGameConstants : IClortonGameConstants
    {
        /// <summary>
        /// The text the arbiter thread writes on the termination condition detection
        /// and the reader threads seek this text
        /// </summary>
        public string LookForText => TheLookForText;
        /// <summary>
        /// The char written by the x writer thread
        /// </summary>
        public char XChar => TheXChar;
        /// <summary>
        /// The char written by the x writer thread
        /// </summary>
        public char OChar => TheOChar;

        
        private const string TheLookForText = "CLORTON";
        private const char TheXChar = 'x';
        private const char TheOChar = 'o';
    }

    /// <summary>
    /// An interface that varied versions of the clorton game implement
    /// </summary>
    public interface IClortonGame : IDisposable
    {
        /// <summary>
        /// raised when the game ends
        /// </summary>
        event EventHandler<ClortonGameEndedEventArgs> GameEnded;

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
        /// Number of pending reader threads
        /// </summary>
        int PendingReaderThreads { get; }

       
    }
}