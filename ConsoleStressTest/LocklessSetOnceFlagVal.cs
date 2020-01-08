using System.Threading;

namespace ConsoleStressTest
{
    public struct LocklessSetOnceFlagVal
    {
        public static implicit operator bool(LocklessSetOnceFlagVal convertMe) => convertMe.IsSet;

        public bool IsSet => _state != NotSet;

        public bool TrySet()
        {
            const int mustBeNow = NotSet;
            const int willBeOnSuccess = Set;
            int actuallyNow = Interlocked.CompareExchange(ref _state, willBeOnSuccess, mustBeNow);
            return actuallyNow == mustBeNow;
        }

        private volatile int _state;
        private const int Set = NotSet + 1;
        private const int NotSet = default;
    }
}