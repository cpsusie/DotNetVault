namespace DotNetVault.DisposeFlag
{
    /// <summary>
    /// A dispose flag like unto <see cref="IDisposeFlag"/> save that it has 3 states:
    /// 1- NotSet
    /// 2- Disposing
    /// 3- Disposed
    /// </summary>
    public interface ITwoStepDisposeFlag 
    {
        /// <summary>
        /// True if the flag's state is set to Disposed
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// True if the flag's state is set to Disposing 
        /// </summary>
        bool IsDisposing { get; }
        /// <summary>
        /// True if the flag's state is set to NotSet
        /// </summary>
        bool IsClear { get; }
        /// <summary>
        /// Try to set the flag's state to Disposing.
        /// Only works if state currently NotSet
        /// </summary>
        /// <returns>True if the state was changed from NotSet to Disposing,
        /// false otherwise (i.e. call did nothing bc not in correct starting state)</returns>
        bool SignalDisposeBegin();
        /// <summary>
        /// Try to set the flag's state to NotSet
        /// </summary>
        /// <returns>True if the state changed from Disposing to NotSet,
        /// false otherwise (i.e. call did nothing bc not in correct starting state)</returns>
        bool SignalDisposeCancelled();
        /// <summary>
        /// Try to set the flag's state to Disposed
        /// </summary>
        /// <returns>True if the state changed from Disposing to Disposed,
        /// false otherwise (i.e. call did nothing bc not in correct starting state)</returns>
        bool SignalDisposed();
    }
}