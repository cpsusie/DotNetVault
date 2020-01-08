using System;
using System.Threading;
using JetBrains.Annotations;

namespace ConsoleStressTest
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
}