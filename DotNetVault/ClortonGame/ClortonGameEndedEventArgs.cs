using System;
using DotNetVault.Logging;
using JetBrains.Annotations;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// When the game is finally ended, these args give you the full results.
    /// </summary>
    public sealed class ClortonGameEndedEventArgs : EventArgs
    {
        #region Properties
        /// <summary>
        /// Results found here.
        /// </summary>
        public ref readonly ClortonGameResult Results => ref _result; 
        #endregion

        #region CTOR
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="start">start time</param>
        /// <param name="end">end time</param>
        /// <param name="cancel">cancelled?</param>
        /// <param name="final">final string</param>
        /// <param name="xCount">x count</param>
        /// <param name="oCount">o count</param>
        /// <param name="winningThreadIdx">winning reader thread idx,
        /// null means no winner.</param>
        /// <exception cref="ArgumentNullException"><paramref name="final"/> was <see langword="null"/>
        /// </exception>
        public ClortonGameEndedEventArgs(DateTime start, DateTime end, bool cancel, [NotNull] string final, int xCount,
            int oCount, int? winningThreadIdx)
        {
            _result = new ClortonGameResult(start, end, cancel, final, xCount, oCount, winningThreadIdx);
            _stringRep = new LocklessWriteOnce<string>(GetStringRep); //string rep lazy init.
        }
        #endregion

        #region Methods
        /// <inheritdoc />
        public override string ToString() => _stringRep.Value; 
        #endregion

        #region Private Method
        private string GetStringRep() =>
           "This game lasted " + ((_result.EndedAt - _result.StartedAt)).TotalMilliseconds.ToString("F6") +
           " milliseconds.  " + (_result.Cancelled ? "It was terminated prematurely.  " : string.Empty) +
           "The final count of " + ClortonGame.GameConstants.XChar + " was " + _result.XCount + ".  The final count of " +
           ClortonGame.GameConstants.OChar + " was " + _result.OCount + "." + (_result.Success
               ? "  The game was successful.  Thread with idx " + _result.WinningThreadIndex + " was the winner."
               : "  The game was unsuccessful.");
        #endregion

        #region Privates
        private readonly ClortonGameResult _result;
        private readonly LocklessWriteOnce<string> _stringRep; 
        #endregion
    }
}