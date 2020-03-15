using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
[assembly: InternalsVisibleTo("VaultUnitTests")]
namespace DotNetVault.Logging
{
    internal struct SetOnceValFlag
    {
        public static implicit operator bool(in SetOnceValFlag flag) => flag.IsSet;

        public readonly bool IsSet
        {
            get
            {
                int isSet = _isSet;
                return isSet != Clear;
            }
        }

        public bool TrySet()
        {
            const int wantToBe = Set;
            const int needToBeNow = Clear;
            return 
                Interlocked.CompareExchange(ref _isSet, wantToBe, needToBeNow) 
                == needToBeNow;
        }

        public void SetOrThrow()
        {
            bool ok = TrySet();
            if (!ok) throw new InvalidOperationException("Flag already set.");
        }

        public override readonly string ToString()
        {
            bool b = IsSet;
            return b.ToString();
        }

        private volatile int _isSet;
        private const int Clear = 0;
        private const int Set = 1;
    }

    internal sealed class SetOnceFlag
    {
        public bool IsSet
        {
            get
            {
                int isSet = _isSet;
                return isSet != Clear;
            }
        }

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
