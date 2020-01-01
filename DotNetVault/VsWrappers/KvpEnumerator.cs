using System;
using System.Collections;
using System.Collections.Generic;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.VsWrappers
{
    /// <summary>
    /// A wrapper considered vault safe (but not suitable as a protected resource stored in a vault)
    /// around a generic IEnumerator object of key value pairs that have both keys and values that are vault-safe
    /// </summary>
    /// <typeparam name="TKey">the vault-safe type of key component of the kvps</typeparam>
    /// <typeparam name="TValue">the vault-safe type of value component of the kvps</typeparam>
    [NotVsProtectable]
    [VaultSafe(true)]
    public struct KvpEnumerator<[VaultSafeTypeParam] TKey, [VaultSafeTypeParam] TValue> : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        #region Factory

        /// <summary>
        /// Wrap an IEnumerator of a <see cref="KeyValuePair{TKey,TValue}"/> (where both TKey and TValue are VaultSafe types)
        /// for purposes of the Query/MixedAction/Action delegates.
        /// </summary>
        /// <param name="toWrap">the enumerator to wrap</param>
        /// <typeparam name="TQKey">vault-safe key type</typeparam>
        /// <typeparam name="TQValue">vault-safe value type</typeparam>
        /// <returns>A wrapper that is considered vault-safe.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="toWrap"/> was null.</exception>
        public static KvpEnumerator<TQKey, TQValue> CreateStandardEnumerator<[VaultSafeTypeParam] TQKey,
            [VaultSafeTypeParam] TQValue>([NotNull] IEnumerator<KeyValuePair<TQKey, TQValue>> toWrap)
            => new KvpEnumerator<TQKey, TQValue>(toWrap ?? throw new ArgumentNullException(nameof(toWrap))); 
        #endregion

        #region Properties
        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current => _current;
        object IEnumerator.Current => _ok ?
            Current
            : throw new InvalidOperationException("The enumerator does not refer to an object.");
        #endregion

        #region Public
        /// <inheritdoc />
        public void Reset()
        {
            _enumerator?.Reset();
            _current = default;
        }
        /// <summary>Advances the enumerator to the next element of the collection.</summary>
        /// <returns>true if the enumerator was successfully advanced to the next element;
        /// false if the enumerator has passed the end of the collection.</returns>
        public bool MoveNext()
        {
            bool ok = _enumerator?.MoveNext() ?? false;
            _current = ok ? _enumerator.Current : default;
            return (_ok = ok);
        }
        #endregion

        #region Expl intf impl
        void IDisposable.Dispose() => _enumerator?.Dispose();
        /// <summary>Advances the enumerator to the next element of the collection.</summary>
        /// <returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.</returns>
        /// <exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created.</exception>
        bool IEnumerator.MoveNext() => MoveNext();
        #endregion

        #region Private CTOR
        private KvpEnumerator([NotNull] IEnumerator<KeyValuePair<TKey, TValue>> toWrap)
        {
            _enumerator = toWrap ?? throw new ArgumentNullException(nameof(toWrap));
            _ok = false;
            _current = default;
        }

        #endregion

        #region Private Data
        private bool _ok;
        private KeyValuePair<TKey, TValue> _current;
        private readonly IEnumerator<KeyValuePair<TKey, TValue>> _enumerator;
        #endregion
    }
}