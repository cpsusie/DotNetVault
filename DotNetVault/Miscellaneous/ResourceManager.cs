using System;
using System.Threading;

namespace DotNetVault.Miscellaneous
{
    internal sealed class ResourceManager<T> where T : class
    {
        public bool IsSet => _value != null;
        public bool IsDisposed => _disposedFlag.IsDisposed;

        /// <summary>
        /// Get the current value
        /// </summary>
        /// <exception cref="ObjectDisposedException"> object is disposed</exception>
        /// <exception cref="InvalidOperationException">value has never been set</exception>
        public T Value
        {
            get
            {
                if (_disposedFlag.IsDisposed) throw new ObjectDisposedException(nameof(ResourceManager<T>));
                var getResult = TryGetValue();
                if (getResult.Success)
                {
                    return getResult.Value;
                }
                throw new InvalidOperationException("The value is not currently set.");
            }
        }

        public (bool Success, T Value) TryGetValue()
        {
            if (_disposedFlag.IsDisposed) throw new ObjectDisposedException(nameof(ResourceManager<T>));

            T value = _value;
            return (value != null, value);
        }

        public void Dispose() => Dispose(true);

        public void SetNewValue(T newValue)
        {
            if (newValue == null)
            {
                throw new ArgumentNullException(nameof(newValue));
            }
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ResourceManager<T>));
            }
            var oldValue = Interlocked.Exchange(ref _value, newValue);
            if (!ReferenceEquals(oldValue, newValue) && oldValue is IDisposable d)
            {
                try
                {
                    d.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
        }
        private void Dispose(bool disposing)
        {
            if (disposing && _disposedFlag.TryDispose())
            {
                var disposeMe = Interlocked.Exchange(ref _value, null);
                if (disposeMe is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }



        private DisposedFlag _disposedFlag = default;
        private volatile T _value;
    }
}