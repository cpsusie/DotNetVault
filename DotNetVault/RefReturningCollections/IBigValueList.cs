using System;
using DotNetVault.Attributes;

namespace DotNetVault.RefReturningCollections
{
    /// <summary>
    /// Interface for a big value type list
    /// </summary>
    /// <typeparam name="TValue">the value type</typeparam>
    public interface IBigValueList<[VaultSafeTypeParam] TValue> : IByRefList<TValue>, IBigValueReadOnlyList<TValue> where TValue : struct, IEquatable<TValue>, IComparable<TValue>
    {
        /// <summary>
        /// Sort the list using the specified comparer
        /// </summary>
        /// <param name="comparer">the comparer</param>
        /// <typeparam name="TComparer">The type of the comparer</typeparam>
        /// <exception cref="BadComparerException{TComparer,TValue}"><paramref name="comparer"/> was not initialized property and does not work
        /// when default constructed.</exception>
        void Sort<TComparer>(in TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Sort the list using the specified comparer after default constructing the specified comparer
        /// </summary>
        /// <typeparam name="TComparer">The comparer -- must be an unmanaged value type that works correctly when default constructed.</typeparam>
        /// <exception cref="Exception"><typeparamref name="TComparer"/> does not work correctly when default constructed.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}"> Comparers of type <typeparamref name="TComparer"/> do not work when default constructed.
        /// </exception>
        void Sort<TComparer>() where TComparer : struct, IByRefCompleteComparer<TValue>;
    }
}