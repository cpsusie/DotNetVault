using System;
using System.Collections;
using System.Collections.Generic;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.VsWrappers
{
    /// <summary>
    /// A wrapper considered vault safe (but not suitable as a protected resource stored in a vault)
    /// around a generic IEnumerator object of a VaultSafe type
    /// </summary>
    /// <typeparam name="T">the vault-safe type of the enumerator</typeparam>
    [NotVsProtectable]
    [VaultSafe(true)]
    public struct StandardEnumerator<[VaultSafeTypeParam] T> : IEnumerator<T>
    {
        /// <summary>
        /// Wrap an IEnumerator of VaultSafe type TQ in a wrapper that is considered VaultSafe
        /// for purposes of the Query/MixedAction/Action delegates.
        /// </summary>
        /// <param name="toWrap">the enumerator to wrap</param>
        /// <typeparam name="TQ">the vault-safe type this IEnumerator enumerates</typeparam>
        /// <returns>A wrapper that is considered vault-safe.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="toWrap"/> was null.</exception>
        public static StandardEnumerator<TQ> CreateStandardEnumerator<[VaultSafeTypeParam] TQ>([NotNull] IEnumerator<TQ> toWrap)
        {
            if (toWrap == null) throw new ArgumentNullException(nameof(toWrap));
            return new StandardEnumerator<TQ>(toWrap);
        }

        #region Properties

        /// <inheritdoc />
        public T Current => _current;
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
        private StandardEnumerator([NotNull] IEnumerator<T> toWrap)
        {
            _enumerator = toWrap ?? throw new ArgumentNullException(nameof(toWrap));
            _ok = false;
            _current = default;
        }

        #endregion

        #region Private Data
        private bool _ok;
        private T _current;
        private readonly IEnumerator<T> _enumerator;
        #endregion

    }
}