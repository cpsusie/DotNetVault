using System;
using System.Collections;
using System.Collections.Generic;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.VsWrappers
{
    /// <summary>
    /// a wrapper around an array enumerator of a vault-safe type that can be used
    /// in the VaultQuery, VaultAction, VaultMixedOperation delegates
    /// </summary>
    /// <typeparam name="T">the vault-safe type held by the array.</typeparam>
    [NotVsProtectable]
    [VaultSafe(true)]
    public struct ArrayEnumeratorWrapper<[VaultSafeTypeParam] T> : IEnumerator<T>
    {
        /// <summary>
        /// Wrap an array's enumerator of a vault-safe type in a wrapper that can be used
        /// in the VaultQuery, VaultAction, VaultMixedOperation delegates
        /// </summary>
        /// <param name="arr">the array whose enumerator you wish to wrap</param>
        /// <typeparam name="TQ">The vault-safe type of the array</typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ArrayEnumeratorWrapper<TQ> CreateArrayEnumeratorWrapper<[VaultSafeTypeParam] TQ>([NotNull] TQ[] arr)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            return new ArrayEnumeratorWrapper<TQ>(arr);
        }

        /// <inheritdoc />
        public T Current => _current;
        object IEnumerator.Current => _idx.HasValue && _idx.Value >= 0 && (_idx.Value < _array.Length)
            ? Current
            : throw new InvalidOperationException("The enumerator does not refer to an object.");

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (_array != null)
            {
                if (_idx == null)
                {
                    _idx = 0;
                }
                else
                {
                    ++_idx;
                }

                if (_idx >= 0 && _idx < _array.Length)
                {
                    _current = _array[_idx.Value];
                    return true;
                }
            }
            _current = default;
            return false;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _idx = null;
            _current = default;
        }

        private ArrayEnumeratorWrapper([NotNull] T[] getMyEnumerator)
        {
            _array = getMyEnumerator;
            _idx = null;
            _current = default;
        }

        void IDisposable.Dispose() { }

        private int? _idx;
        private T _current;
        private readonly T[] _array;
    }
}