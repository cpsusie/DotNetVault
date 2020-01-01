using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class LocklessSetClearDispose<T> : IDisposable where T : class
    {
        public bool HasValue => _resource != null;
        public bool IsDisposed => _isDisposed.IsSet;

        /// <summary>
        /// Get the value if set.  Illegal to call if not.
        /// </summary>
        /// <exception cref="InvalidOperationException">Accessed before set.</exception>
        public T Value => _resource;

        public bool TrySet([NotNull] T valueToSet)
        {
            if (valueToSet == null) throw new ArgumentNullException(nameof(valueToSet));

            T shouldBeNull = Interlocked.CompareExchange(ref _resource, valueToSet, null);
            return shouldBeNull == null;
        }

        /// <summary>
        /// Try to release the resource WITHOUT disposing it
        /// </summary>
        /// <returns>The released resource and a value indicating success or failure.  Accessing ReleasedVal if !Success
        /// will result in undefined behavior</returns>
        public (bool Success, T ReleasedVal) TryRelease()
        {
            T currentAtStart = _resource;
            T valueReplacedWithNullIfSameAsCurrentAtStart =
                Interlocked.CompareExchange(ref _resource, null, currentAtStart);
            bool ret = currentAtStart == valueReplacedWithNullIfSameAsCurrentAtStart;
            return (ret && currentAtStart != null, currentAtStart);
        }

        public bool TryClear()
        {
            T currentAtStart = _resource;
            T valueReplacedWithNullIfSameAsCurrentAtStart =
                Interlocked.CompareExchange(ref _resource, null, currentAtStart);
            bool ret = currentAtStart == valueReplacedWithNullIfSameAsCurrentAtStart;
            if (ret && valueReplacedWithNullIfSameAsCurrentAtStart is IDisposable d)
            {
                try
                {
                    d.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }
            }
            return ret;
        }

        public void SetOrThrow([NotNull] T valueToSet)
        {
            if (valueToSet == null) throw new ArgumentNullException(nameof(valueToSet));

            if (!TrySet(valueToSet)) throw new InvalidOperationException("The value has already been set.");
        }

        public void Dispose() => Dispose(true);
        private void Dispose(bool disposing)
        {
            if (disposing && _isDisposed.TrySet())
            {
                var resource = Interlocked.Exchange(ref _resource, null);
                if (resource is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLineAsync(e.ToString());
                    }
                }
            }
        }

        private volatile T _resource;
        private LocklessSetOnceFlagVal _isDisposed = new LocklessSetOnceFlagVal();
    }
    public sealed class LocklessWriteOnce<T> where T : class
    {
        /// <summary>
        /// True if set, false otherwise.  True is conclusively and permanently true.
        /// False can change at any moment and cannot be relied on.
        /// </summary>
        public bool IsSet => _value != null;

        /// <summary>
        /// Get the value if set.  Illegal to call if not.
        /// </summary>
        /// <exception cref="InvalidOperationException">Accessed before set.</exception>
        [NotNull]
        public T Value
        {
            get
            {
                T now = _value;
                if (now == null) throw new InvalidOperationException("The value has not been set.");
                return now;
            }
        }

        public bool TrySet([NotNull] T valueToSet)
        {
            if (valueToSet == null) throw new ArgumentNullException(nameof(valueToSet));

            T shouldBeNull = Interlocked.CompareExchange(ref _value, valueToSet, null);
            return shouldBeNull == null;
        }

        public void SetOrThrow([NotNull] T valueToSet)
        {
            if (valueToSet == null) throw new ArgumentNullException(nameof(valueToSet));

            if (!TrySet(valueToSet)) throw new InvalidOperationException("The value has already been set.");
        }

        public (bool IsSet, T Value) TryGetValue()
        {
            T val = _value;
            return (val != null, val);
        }

        private volatile T _value;
    }
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