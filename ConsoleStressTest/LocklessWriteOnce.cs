using System;
using System.Threading;
using JetBrains.Annotations;

namespace ConsoleStressTest
{
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
}