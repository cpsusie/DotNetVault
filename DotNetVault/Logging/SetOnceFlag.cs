using System.Diagnostics;
using System.Threading;

namespace DotNetVault.Logging
{
    internal sealed class SetOnceFlag
    {
        public bool IsSet => _isSet != Clear;

        public bool TrySet()
        {
            var oldValue = Interlocked.CompareExchange(ref _isSet, Set, Clear);
            Debug.Assert(IsSet);
            return oldValue == Clear;
        }

        private volatile int _isSet;
        private const int Clear = 0;
        private const int Set = 1;
    }
}
