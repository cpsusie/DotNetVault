using System;
using DotNetVault.Logging;

namespace VaultUnitTests.ClortonGame
{
    public sealed class ClortonGameEndedEventArgs : EventArgs
    {
        public ref readonly ClortonGameResult Results => ref _result;

        public ClortonGameEndedEventArgs(DateTime start, DateTime end, bool cancel, string final, int xCount,
            int oCount, int? winningThreadIdx)
        {
            _result = new ClortonGameResult(start, end, cancel, final, xCount, oCount, winningThreadIdx);
            _stringRep = new LocklessWriteOnce<string>(GetStringRep);
        }

        public override string ToString() => _stringRep.Value;

        private string GetStringRep() =>
            "This game lasted " + ((_result.EndedAt - _result.StartedAt)).TotalMilliseconds.ToString("F6") +
            " milliseconds.  " + (_result.Cancelled ? "It was terminated prematurely.  " : string.Empty) +
            "The final count of " + ClortonGame.XChar + " was " + _result.XCount + ".  The final count of " +
            ClortonGame.OChar + " was " + _result.OCount + "." + (_result.Success
                ? "  The game was successful.  Thread with idx " + _result.WinningThreadIndex + " was the winner."
                : "  The game was unsuccessful.");

        private readonly ClortonGameResult _result;
        private LocklessWriteOnce<string> _stringRep;
    }
}