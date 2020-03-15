using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using HpTimesStamps;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace VaultUnitTests.ClortonGame
{
    sealed class ReaderThread : ClortonGameThread<(DateTime TimeStamp, bool FoundIt)>
    {
        public event EventHandler<ClortonGameFinishedEventArgs> Finished;

        public override (DateTime TimeStamp, bool FoundIt)? Result
        {
            get
            {
                lock (_syncRoot)
                {
                    return _result != null ? (_result.Value, _foundIt) : ((DateTime Value, bool _foundIt)?) null;
                }
            }
        }

        public ReaderThread([NotNull] BasicReadWriteVault<string> vault, [NotNull] ITestOutputHelper helper, int num, [NotNull] string lookFor) : base(vault, helper)
        {
            _idx = num;
            _lookFor = lookFor ?? throw new ArgumentNullException(nameof(lookFor));
        }

        protected override void ExecuteJob(CancellationToken token)
        {
            DateTime? timestamp = null;
            while (timestamp == null)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var lck = _vault.RoLock(token);
                    timestamp = lck.Value.Contains(_lookFor) ? (DateTime?) TimeStampSource.Now : null;
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
                    DateTime errTime = TimeStampSource.Now;
                    _helper.WriteLine($"At [{errTime:O}], reader with idx {_idx} timed out.  Ex: [{ex}].");
                }
                catch (Exception ex)
                {
                    _tempTime = TimeStampSource.Now;
                    _helper.WriteLine($"At [{_tempTime:O}], reader with {_idx} faulted. Ex: [{ex}].");
                    _tempFoundIt = false;
                    throw;
                }
            }
            
        }

        protected override void PerformFinishingActions()
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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_localDisp.TrySet() && disposing)
            {
                Finished = null;
            }
        }
        
        protected override Thread InitThread() => new Thread(ThreadLoop){IsBackground = true, Name = $"Rdr#_{_idx.ToString()}"};
        private void OnFinished(ClortonGameFinishedEventArgs e)
        {
            EventHandler<ClortonGameFinishedEventArgs> handler = Finished;
            if (e != null && handler != null)
            {
                Task.Run(() =>
                    handler(this, e));
            }
        }
        private DateTime? _tempTime;
        private bool _tempFoundIt; 

        private SetOnceValFlag _localDisp = default;
        private readonly string _lookFor;
        private readonly int _idx;
        private DateTime? _result;
        private bool _foundIt;
        private readonly object _syncRoot = new object();
        

       
    }
}