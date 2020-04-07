using System;
using System.Threading;
using DotNetVault.CustomVaultExamples.CustomVaults;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Factory to create Test output helpers
    /// </summary>
    public readonly struct TestOutputHelperFactory
    {
        /// <summary>
        /// Create a helper based on a string builder
        /// </summary>
        /// <returns>a helper</returns>
        public IBufferBasedOutputHelper CreateHelper() => new StringBuilderBasedTestOutputHelper();

        #region Nested Type Defs
        private sealed class StringBuilderBasedTestOutputHelper : IBufferBasedOutputHelper
        {
            public bool IsDisposed => _disposed.IsSet;

            public void WriteLine(string message)
            {
                ThrowIfDisposed();
                using var lck = _sbVault.Lock();
                lck.AppendLine(message);
            }

            public void WriteLine(string format, params object[] args)
            {
                ThrowIfDisposed();
                using var lck = _sbVault.Lock();
                lck.AppendLine(string.Format(format, args));
            }

            public void Dispose() => Dispose(true);


            public string GetCurrentText(TimeSpan timeout)
            {
                ThrowIfDisposed();
                using var lck = _sbVault.Lock(timeout);
                return lck.ToString();
            }

            public string GetCurrentTextAndClearBuffer(TimeSpan timeout)
            {
                ThrowIfDisposed();
                using var lck = _sbVault.Lock(timeout);
                string ret = lck.ToString();
                lck.Clear();
                return ret;
            }

            private void Dispose(bool disposing)
            {
                if (_disposed.TrySet() && disposing)
                {
                    try
                    {
                        _sbVault.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync(ex.ToString());
                        _disposed.TryClear();
                        throw;
                    }
                }

            }

            private void ThrowIfDisposed()
            {
                if (_disposed.IsSet)
                    throw new ObjectDisposedException(nameof(StringBuilderBasedTestOutputHelper));
            }

            private readonly StringBuilderVault _sbVault =
                new StringBuilderVault(TimeSpan.FromMilliseconds(100));

            private ToggleFlag _disposed = default;
        }

        internal struct ToggleFlag
        {
            public bool IsSet
            {
                get
                {
                    int val = _value;
                    return val == Set;
                }
            }

            public bool IsClear => !IsSet;

            public bool TrySet()
            {
                const int wantToBe = Set;
                const int needToBeNow = Clear;
                return Interlocked.CompareExchange(ref _value, wantToBe, needToBeNow) == needToBeNow;
            }

            public bool TryClear()
            {
                const int wantToBe = Clear;
                const int needToBeNow = Set;
                return Interlocked.CompareExchange(ref _value, wantToBe, needToBeNow) == needToBeNow;
            }


            private volatile int _value;
            private const int Clear = default;
            private const int Set = 1;
        } 
        #endregion
    }
}