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


        private static volatile ILogProvider _provider;
    }
}