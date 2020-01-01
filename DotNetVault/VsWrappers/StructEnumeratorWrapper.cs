using System;
using System.Collections;
using System.Collections.Generic;
using DotNetVault.Attributes;

namespace DotNetVault.VsWrappers
{
    /// <summary>
    /// A mutable wrapper around a struct enumerator of <typeparamref name="TItem"/> where T is a vault-safe type
    /// allowing it to be used to in the query/action delegates
    /// </summary>
    /// <typeparam name="TWrappedEnumerator">The type of the enumerator to wrap.  Must be a struct.</typeparam>
    /// <typeparam name="TItem">The type the enumerator enumerates, must be vault safe</typeparam>
    [NotVsProtectable]
    [VaultSafe(true)]
    public struct StructEnumeratorWrapper<TWrappedEnumerator, [VaultSafeTypeParam] TItem>
        : IEnumerator<TItem> where TWrappedEnumerator : struct, IEnumerator<TItem>
    {
        #region Static Factory
        /// <summary>
        /// Convert a struct enumerator of vault-safe type TQ into a an enumerator
        /// that can be used in the action/query delegates
        /// </summary>
        /// <param name="enumerator">enumerator to wrap</param>
        /// <typeparam name="TQWrappedEnumerator">The struct enumerator type you want to wrap.</typeparam>
        /// <typeparam name="TQItem">the vault safe item type the enumerator enumerates.</typeparam>
        /// <returns>the wrapper</returns>
        public static StructEnumeratorWrapper<TQWrappedEnumerator, TQItem> CreateWrapper<TQWrappedEnumerator,
            [VaultSafeTypeParam] TQItem>(TQWrappedEnumerator enumerator)
            where TQWrappedEnumerator : struct, IEnumerator<TQItem>
        {
            var ret = new StructEnumeratorWrapper<TQWrappedEnumerator, TQItem>();
            ret._enumerator = enumerator;
            return ret;
        }
        #endregion

        #region Public Props
        /// <inheritdoc />
        public TItem Current => _enumerator.Current;
        object IEnumerator.Current => Current;
        #endregion

        #region Methods
        /// <inheritdoc />
        public void Reset() => _enumerator.Reset();
        /// <summary>Advances the enumerator to the next element of the collection.</summary>
        /// <returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.</returns>
        public bool MoveNext() => _enumerator.MoveNext();
        #endregion

        #region Expl Intf Impl
        void IDisposable.Dispose() => _enumerator.Dispose();
        bool IEnumerator.MoveNext() => MoveNext();
        #endregion

        #region Fields 
        private TWrappedEnumerator _enumerator;
        #endregion
    }
}