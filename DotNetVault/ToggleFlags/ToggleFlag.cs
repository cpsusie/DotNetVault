using System;
using System.Threading;

namespace DotNetVault.ToggleFlags
{
    internal sealed class ToggleFlag : IToggleFlag
    {
        public bool IsSet => _state == Set;
        public bool IsClear => _state == Clear;

        public ToggleFlag() : this(false) { }

        public ToggleFlag(bool beginSet) => _state = beginSet ? Set : Clear;

        public bool SetFlag()
        {
            const int wantToSet = Set;
            int comparand = Clear;
            int oldState = Interlocked.CompareExchange(ref _state, wantToSet, comparand);
            return oldState == Clear;
        }

        public bool ClearFlag()
        {
            const int wantToSet = Clear;
            int comarand = Set;
            int oldState = Interlocked.CompareExchange(ref _state, wantToSet, comarand);
            return oldState == Clear;
        }

        private const int Clear = 0;
        private const int Set = 1;
        private volatile int _state;
    }

    internal struct SetOnceValFlag
    {
        public bool IsSet
        {
            get
            {
                int val = _value;
                return val == Set;
            }
        }

        public bool IsClear
        {
            get
            {
                int val = _value;
                return val == NotSet;
            }
        }

        public bool TrySet()
        {
            const int wantToBe = Set;
            const int needToBeNow = NotSet;
            return Interlocked.CompareExchange(ref _value, wantToBe, needToBeNow) == needToBeNow;
        }

        public void SetOrThrow()
        {
            bool ok = TrySet();
            if (!ok) throw new InvalidOperationException("The flag has already been set.");
        }

        private volatile int _value;
        private const int NotSet = 0;
        private const int Set = 1;
    }
}
