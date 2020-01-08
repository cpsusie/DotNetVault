using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using JetBrains.Annotations;

namespace ConsoleStressTest
{
    [VaultSafe(true)]
    public sealed class LocklessLazyWriteOnce<T> where T : class
    {
        public static implicit operator T([NotNull] LocklessLazyWriteOnce<T> convertMe) =>
            (convertMe ?? throw new ArgumentNullException(nameof(convertMe))).Value;

        public bool IsSet => _value != null;

        public T Value
        {
            get
            {
                T val = _value;
                if (val == null)
                {
                    try
                    {
                        val = _ctor();
                        if (val == null)
                        {
                            throw new DelegateReturnedNullException<Func<T>>(_ctor, nameof(_ctor));
                        }
                    }
                    catch (DelegateException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new DelegateThrewException<Func<T>>(_ctor, nameof(_ctor), ex);
                    }

                    T oldVal = Interlocked.CompareExchange(ref _value, val, null);
                    if (oldVal != null && val is IDisposable d)
                    {
                        try
                        {
                            d.Dispose();
                        }
                        catch
                        {
                            //ignore
                        }
                    }
                    val = _value;
                }
                Debug.Assert(val != null && ReferenceEquals(val, _value));
                return val;
            }
        }

        public bool TrySetToAlternateValue([NotNull] T alternate)
        {
            if (alternate == null) throw new ArgumentNullException(nameof(alternate));
            T current = Interlocked.CompareExchange(ref _value, alternate, null);
            return current == null;
        }

        public LocklessLazyWriteOnce([NotNull] Func<T> lazyCtor) =>
            _ctor = lazyCtor ?? throw new ArgumentNullException(nameof(lazyCtor));



        private readonly Func<T> _ctor;
        private volatile T _value;
    }
}