using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetVault.ClortonGame;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Reader thread that participates in the CafeBabe game
    /// </summary>
    public sealed class ReaderThread : CafeBabeGameThread
    {
        #region Properties and Events
        /// <summary>
        /// Raised when the thread finished
        /// </summary>
        public event EventHandler<CafeBabeGameFinishedEventArgs> Finished;
        /// <summary>
        /// Nullable result object.  
        ///
        /// Timestamp -- when result obj created
        /// Found it  -- true if it found it, false otherwise
        /// LocatedIndex -- the index where it found the value it sought, if found; if it didn't, null.
        /// </summary>
        public (DateTime TimeStamp, bool FoundIt, int? LocatedIndex)? Result
        {
            get
            {
                lock (_syncRoot)
                {
                    return _result != null ? (_result.Value, _foundIt, _locatedIndex)
                        : ((DateTime Value, bool _foundIt, int? idx)?)null;
                }
            }
        }
        #endregion

        #region Public CTOR
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="vault">the resource vault</param>
        /// <param name="helper">helper for looking</param>
        /// <param name="num">the idx of the reader thread in the collection of reader threads.</param>
        ///  <exception cref="ArgumentNullException"><paramref name="vault"/> or <paramref name="helper"/>
        /// were <see langword="null"/>.</exception>
        public ReaderThread([NotNull] ReadWriteValueListVault<UInt256> vault, [NotNull] IOutputHelper helper, int num)
            : base(vault, helper) => _idx = num; 
        #endregion
        
        #region Protected Methods
        /// <inheritdoc />
        protected override void ExecuteJob(CancellationToken token)
        {
            DateTime? timestamp = null;
            while (timestamp == null)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    int count;
                    int identifiedIndex;
                    {
                        using var lck = _valueList.RoLock(token); //obtain readonly lock
                        count = lck.Count;
                        identifiedIndex = count > 0 ? lck.IndexOf<UInt256CompleteComparer>(in LookFor) : -1;
                        timestamp = identifiedIndex > -1
                            ? (DateTime?) CgTimeStampSource.Now
                            : null; //check to see if string contains
                        if (timestamp != null)
                        {
                            _tempLocatedIndex = identifiedIndex;
                            _tempTime = timestamp;
                            _tempFoundIt = true;
                        }
                    }
#if DEBUG
                    _helper.WriteLine(
                        $"Reader thread # {_idx} scanned {count} items.  It {(identifiedIndex > -1 ? $"found it at idx {identifiedIndex}." : "did not find it.")}");
#endif

                }
                catch (OperationCanceledException)
                {
                    _tempTime = CgTimeStampSource.Now;
                    _tempFoundIt = false;
                    throw;
                }
                catch (TimeoutException ex)
                {
                    _helper.WriteLine($"Reader with idx {_idx} timed out.  Ex: [{ex}].");
                }
                catch (Exception ex)
                {
                    _tempTime = CgTimeStampSource.Now;
                    _helper.WriteLine($"Reader with {_idx} faulted. Ex: [{ex}].");
                    _tempFoundIt = false;
                    throw;
                }
            }
        }

        /// <inheritdoc />
        protected override void PerformFinishingActions()
        {
            CafeBabeGameFinishedEventArgs args;
            lock (_syncRoot)
            {
                _locatedIndex = _tempLocatedIndex;
                _foundIt = _tempFoundIt;
                _result = _tempTime ?? CgTimeStampSource.Now;
                args = new CafeBabeGameFinishedEventArgs(_result.Value, _foundIt, _idx, _locatedIndex);
            }
            OnFinished(args);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_localDisp.TrySet() && disposing)
            {
                Finished = null;
            }
        }

        /// <inheritdoc />
        protected override Thread InitThread() =>
            new Thread(ThreadLoop) { IsBackground = true, Name = $"Rdr#_{_idx}" }; 
        #endregion

        #region private methods
        private void OnFinished(CafeBabeGameFinishedEventArgs e)
        {
            EventHandler<CafeBabeGameFinishedEventArgs> handler = Finished;
            if (e != null && handler != null)
            {
                Task.Run(() => handler(this, e));
            }
        }
        #endregion

        #region Privates (don't touch tee hee hee!)
        private int? _tempLocatedIndex;
        private int? _locatedIndex;
        private DateTime? _tempTime;
        private bool _tempFoundIt;
        private SetOnceValFlag _localDisp = default;
        private readonly int _idx;
        private DateTime? _result;
        private bool _foundIt;
        private readonly object _syncRoot = new object(); 
        #endregion
    }
}
