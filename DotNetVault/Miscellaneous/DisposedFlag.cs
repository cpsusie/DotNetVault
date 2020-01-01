using System.Diagnostics;
using System.Threading;

namespace DotNetVault.Miscellaneous
{
    internal struct DisposedFlag
    {
        public bool IsDisposed => _current != NotDisposed;

        public bool TryDispose()
        {
            int oldValue = Interlocked.CompareExchange(ref _current, Disposed, NotDisposed);
            Debug.Assert(_current != NotDisposed);
            return oldValue == NotDisposed;
        }

        private const int Disposed = 1;
        private const int NotDisposed = 0;
        private volatile int _current;
    }
}