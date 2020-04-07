using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using TimeStampSource = DotNetVault.ClortonGame.CgTimeStampSource;
namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// A reader thread configured to use <see cref="ReadWriteStringBufferVault"/>.
    /// </summary>
    public sealed class CustomVaultReaderThread : ReaderThread<ReadWriteStringBufferVault>
    {
        /// <inheritdoc />
        public CustomVaultReaderThread([NotNull] ReadWriteStringBufferVault vault, 
            [NotNull] IOutputHelper helper, int num, [NotNull] string lookFor) 
                : base(vault, helper, num, lookFor) {}

        /// <inheritdoc />
        protected override void ExecuteJob(CancellationToken token)
        {
            DateTime? timestamp = null;
            while (timestamp == null)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var lck = _vault.RoLock(token); //obtain readonly lock
                    timestamp = lck.Contains(_lookFor) ? (DateTime?)TimeStampSource.Now : null; //check to see if string contains
                    if (timestamp != null)
                    {
                        _tempTime = timestamp;
                        _tempFoundIt = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    _tempTime = TimeStampSource.Now;
                    _tempFoundIt = false;
                    throw;
                }
                catch (TimeoutException ex)
                {
                    _helper.WriteLine($"Reader with idx {_idx} timed out.  Ex: [{ex}].");
                }
                catch (Exception ex)
                {
                    _tempTime = TimeStampSource.Now;
                    _helper.WriteLine($"Reader with {_idx} faulted. Ex: [{ex}].");
                    _tempFoundIt = false;
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// A reader thread configured to use <see cref="BasicReadWriteVault{T}"/>.
    /// </summary>
    public sealed class BasicVaultReaderThread : ReaderThread<BasicReadWriteVault<string>>
    {
        /// <inheritdoc />
        public BasicVaultReaderThread([NotNull] BasicReadWriteVault<string> vault, [NotNull] IOutputHelper helper, int num, [NotNull] string lookFor) : base(vault, helper, num, lookFor)
        {
        }

        /// <inheritdoc />
        protected override void ExecuteJob(CancellationToken token)
        {
            DateTime? timestamp = null;
            while (timestamp == null)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var lck = _vault.RoLock(token); //obtain readonly lock
                    timestamp = lck.Value.Contains(_lookFor) ? (DateTime?)TimeStampSource.Now : null; //check to see if string contains
                    if (timestamp != null)
                    {
                        _tempTime = timestamp;
                        _tempFoundIt = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    _tempTime = TimeStampSource.Now;
                    _tempFoundIt = false;
                    throw;
                }
                catch (TimeoutException ex)
                {
                    _helper.WriteLine($"Reader with idx {_idx} timed out.  Ex: [{ex}].");
                }
                catch (Exception ex)
                {
                    _tempTime = TimeStampSource.Now;
                    _helper.WriteLine($"Reader with {_idx} faulted. Ex: [{ex}].");
                    _tempFoundIt = false;
                    throw;
                }
            }
        }
    }
    /// <summary>
    /// Demonstrates the read-only lock.  The reader threads
    ///     1. Obtain a read lock.
    ///     2. Search for "Clorton" (hope you find it first)
    /// rinse, repeat
    /// </summary>
    public abstract class ReaderThread<TVault> : ClortonGameThread<(DateTime TimeStamp, bool FoundIt), TVault> where TVault : IBasicVault<string>
    {
        #region Properties / Events
        /// <summary>
        /// Raised when the thread finished
        /// </summary>
        public event EventHandler<ClortonGameFinishedEventArgs> Finished;

        /// <summary>
        /// Nullable result object.  
        ///
        /// Timestamp -- when result obj created
        /// Found it  -- true if it found it, false otherwise
        /// 
        /// </summary>
        public sealed override (DateTime TimeStamp, bool FoundIt)? Result
        {
            get
            {
                lock (_syncRoot)
                {
                    return _result != null ? (_result.Value, _foundIt)
                        : ((DateTime Value, bool _foundIt)?)null;
                }
            }
        } 
        #endregion

        #region CTOR
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="vault">the resource vault</param>
        /// <param name="helper">helper for looking</param>
        /// <param name="num">the idx of the reader thread in the collection of reader threads.</param>
        /// <param name="lookFor">the sub-string string it is looking ("CLORTON") herein</param>
        /// <exception cref="ArgumentNullException"><paramref name="vault"/>, <paramref name="helper"/>
        /// or <paramref name="lookFor"/> were <see langword="null"/>.</exception>
        protected ReaderThread([NotNull] TVault vault, [NotNull] IOutputHelper helper,
            int num, [NotNull] string lookFor) : base(vault, helper)
        {
            _idx = num;
            _lookFor = lookFor ?? throw new ArgumentNullException(nameof(lookFor));
        } 
        #endregion

        #region Methods
        /// <inheritdoc />
        protected sealed override void PerformFinishingActions()
        {
            ClortonGameFinishedEventArgs args;
            lock (_syncRoot)
            {
                _foundIt = _tempFoundIt;
                _result = _tempTime ?? TimeStampSource.Now;
                args = new ClortonGameFinishedEventArgs(_result.Value, _foundIt, _idx);
            }
            OnFinished(args);
        }

        /// <inheritdoc />
        protected sealed override void Dispose(bool disposing)
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

        #region Private Methods
        private void OnFinished(ClortonGameFinishedEventArgs e)
        {
            EventHandler<ClortonGameFinishedEventArgs> handler = Finished;
            if (e != null && handler != null)
            {
                Task.Run(() =>
                    handler(this, e));
            }
        }
        #endregion

        #region Privates
        private protected DateTime? _tempTime;
        private protected bool _tempFoundIt;
        private SetOnceValFlag _localDisp = default;
        private protected readonly string _lookFor;
        private protected readonly int _idx;
        private DateTime? _result;
        private bool _foundIt;
        private readonly object _syncRoot = new object(); 
        #endregion
    }
}