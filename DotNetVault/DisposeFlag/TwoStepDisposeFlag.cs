using System.Threading;

namespace DotNetVault.DisposeFlag
{
    /// <inheritdoc />
    public sealed class DisposeFlag : IDisposeFlag
    {
        /// <inheritdoc />
        public bool IsDisposed => _state == Set;

        /// <inheritdoc />
        public bool IsClear => _state == Clear;

        /// <inheritdoc />
        public bool SignalDisposed()
        {
            int setVal = Set;
            int old = Interlocked.CompareExchange(ref _state, setVal, Clear);
            return old == Clear;
        }
        
        private const int Clear = 0;
        private const int Set = 1;
        private volatile int _state = Clear;
    }

    /// <inheritdoc />
    public sealed class TwoStepDisposeFlag : ITwoStepDisposeFlag
    {
        /// <inheritdoc />
        public bool IsDisposed => _state == Disposed;

        /// <inheritdoc />
        public bool IsDisposing => _state == Disposing;

        /// <inheritdoc />
        public bool IsClear => _state == NotSet;

        /// <inheritdoc />
        public bool SignalDisposeBegin()
        {
            int setVal = Disposing;
            int old = Interlocked.CompareExchange(ref _state, setVal, NotSet);
            return old == NotSet;
        }

        /// <inheritdoc />
        public bool SignalDisposeCancelled()
        {
            int setVal = NotSet;
            int old = Interlocked.CompareExchange(ref _state, setVal, Disposing);
            return old == Disposing;
        }

        /// <inheritdoc />
        public bool SignalDisposed()
        {
            int setVal = Disposed;
            int old = Interlocked.CompareExchange(ref _state, setVal, Disposing);
            return old == Disposing;
        }

        private const int NotSet = 0;
        private const int Disposing = 1;
        private const int Disposed = 2;

        private volatile int _state = NotSet;
    }
}