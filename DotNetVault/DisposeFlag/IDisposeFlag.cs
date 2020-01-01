namespace DotNetVault.DisposeFlag
{
    /// <summary>
    /// An interface representing a dispose flag.
    /// It provides a set-once capability.  
    /// </summary>
    public interface IDisposeFlag
    {
        /// <summary>
        /// True if the flag is set to disposed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// True if the flag is not set to disposed
        /// </summary>
        bool IsClear { get; }

        /// <summary>
        /// Set the flag to disposed.
        /// </summary>
        /// <returns>true if the state changed from clear to disposed,
        /// false if it was already disposed (i.e. no change)</returns>
        bool SignalDisposed();
    }
}