using System;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    internal static class LoggerImpl
    {
        public static ILogProvider Provider
        {
            get
            {
                ILogProvider ret = _provider;
                if (ret == null)
                {
                    ret = LogProvider.CreateInstance();
                    ILogProvider old = Interlocked.CompareExchange(ref _provider, ret, null);
                    if (old != null)
                    {
                        ret.Dispose();
                    }
                    ret = _provider;
                }
                Debug.Assert(ret != null);
                return ret;
            }
        }

        public static void SupplyLogger([NotNull] ILogProvider logProvider)
        {
            if (logProvider == null) throw new ArgumentNullException(nameof(logProvider));

            ILogProvider old = Interlocked.CompareExchange(ref _provider, logProvider, null);
            if (old != null)
            {
                throw new InvalidOperationException("Provider already initialized.");
            }
        }

        public static (bool LoggerExisted, bool LoggerDestroyedByThisCall) DestroyLogger()
        {
            bool loggerDestroyedThisCall;
            ILogProvider provider = _provider;
            bool loggerExisted = provider != null;
            if (loggerExisted && _destroyed.TrySet())
            {
                provider.Dispose();
                loggerDestroyedThisCall = true;
            }
            else
            {
                loggerDestroyedThisCall = false;
            }

            return (loggerExisted, loggerDestroyedThisCall);
        }

        private static LocklessLazySetOnce _destroyed;
        private static volatile ILogProvider _provider;
    }

    internal struct LocklessLazySetOnce
    {
        public static implicit operator bool(in LocklessLazySetOnce convert) => convert.IsSet;
        public readonly bool IsSet
        {
            get
            {
                int code = _code;
                return code == Set;
            }
        }

        public bool TrySet()
        {
            const int wantToBe = Set;
            const int needToBeNow = NotSet;
            return Interlocked.CompareExchange(ref _code, wantToBe, needToBeNow) == needToBeNow;
        }

        public void SetOrThrow()
        {
            if (!TrySet()) 
                throw new InvalidOperationException("Flag already set.");
        }

        public override readonly string ToString() => $"Set once flag status: [{(IsSet ? "SET" : "CLEAR")}].";

        private volatile int _code;
        private const int NotSet = 0;
        private const int Set = 1;
    }
}