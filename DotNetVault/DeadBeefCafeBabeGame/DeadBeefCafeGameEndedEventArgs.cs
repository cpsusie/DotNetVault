using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using DotNetVault.Logging;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// When the game is finally ended, these args give you the full results.
    /// </summary>
    [DataContract]
    public sealed class DeadBeefCafeGameEndedEventArgs : EventArgs
    {
        #region Properties
        /// <summary>
        /// Results found here.
        /// </summary>
        public ref readonly DeadBeefCafeGameResult Results => ref _result;

        /// <summary>
        /// Textual representation of array.
        /// </summary>
        [NotNull]
        public string ArrayText
        {
            get
            {
                string txt = _arrayStringRep;
                if (txt == null)
                {
                    string rep = GetArrayString();
                    Debug.Assert(rep != null);
                    Interlocked.CompareExchange(ref _arrayStringRep, 
                        rep, null);
                    txt = _arrayStringRep;
                }
                Debug.Assert(_arrayStringRep != null);
                return txt;
            }
        }
        #endregion

        #region CTOR

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="start">start time</param>
        /// <param name="end">end time</param>
        /// <param name="cancel">cancelled?</param>
        /// <param name="final">final array</param>
        /// <param name="xCount">x count</param>
        /// <param name="oCount">o count</param>
        /// <param name="lookForNumberFoundAt">the index of <see cref="DeadBeefCafeBabeGameConstants.LookForNumber"/> in the
        /// array, if found.</param>
        /// <param name="winningThreadIdx">winning reader thread idx,
        /// null means no winner.</param>
        /// <exception cref="ArgumentNullException"><paramref name="final"/> was <see langword="null"/>
        /// </exception>
        public DeadBeefCafeGameEndedEventArgs(DateTime start, DateTime end, bool cancel, ImmutableArray<UInt256> final, int xCount,
            int oCount, int? lookForNumberFoundAt, int? winningThreadIdx)
        {
            _result = new DeadBeefCafeGameResult(start, end, cancel, final, xCount, oCount, lookForNumberFoundAt, winningThreadIdx);
            _stringRep = new LocklessWriteOnce<string>(GetStringRep); //string rep lazy init.
            _arrayStringRep = new LocklessWriteOnce<string>(GetArrayString);
        }
        #endregion

        #region Methods

        /// <inheritdoc />
        public override string ToString()
        {
            string txt = _stringRep;
            if (txt == null)
            {
                string rep = GetStringRep();
                Debug.Assert(rep != null);
                Interlocked.CompareExchange(ref _stringRep, rep, null);
                txt = _stringRep;
            }
            Debug.Assert(_stringRep != null);
            return txt;
        }

        #endregion

        #region Private Method
        private string GetStringRep() =>
            "This game lasted " + ((_result.EndedAt - _result.StartedAt)).TotalMilliseconds.ToString("F6") +
            " milliseconds.  " + (_result.Cancelled ? "It was terminated prematurely.  " : string.Empty) +
            "The final count of XChar vals was " + _result.XCount + ".  The final count of OChar vals was " + _result.OCount + "." + (_result.Success
                ? "  The game was successful.  Thread with idx " + _result.WinningThreadIndex + " was the winner and the item was found at: " + Results.NumberFoundAtIndex + "."
                : "  The game was unsuccessful.");
        #endregion

        private string GetArrayString()
        {
            var arr = _result.FinalArray;
            StringBuilder sb = new StringBuilder((arr.Length * (81 + 10)) + 75);
            sb.AppendLine();
            sb.AppendLine("Printing array: ");
            int idx = 0;
            foreach (ref readonly var item in arr)
            {
                sb.AppendLine("Element number " + (++idx) + ":\t\t" + item);
            }
            sb.AppendLine("End printing array.");
            sb.AppendLine();
            return sb.ToString();
        }

        #region Privates
        [DataMember] private readonly DeadBeefCafeGameResult _result;
        private volatile string _stringRep;
        private volatile string _arrayStringRep;
        #endregion
    }
}