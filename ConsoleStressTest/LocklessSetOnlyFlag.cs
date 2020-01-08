using System.Threading;

namespace ConsoleStressTest
{
    public sealed class LocklessSetOnlyFlag
    {
        public bool IsSet => _state != NotSet;

        public bool TrySet()
        {
            const int mustBeNow = NotSet;
            const int willBeOnSuccess = Set;
            int actuallyNow = Interlocked.CompareExchange(ref _state, willBeOnSuccess, mustBeNow);
            return actuallyNow == mustBeNow;
        }

        private volatile int _state = NotSet;
        private const int Set = 1;
        private const int NotSet = 0;
    }
}