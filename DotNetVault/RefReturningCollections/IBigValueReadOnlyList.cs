using System;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;

namespace DotNetVault.RefReturningCollections
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public interface IBigValueReadOnlyList<[VaultSafeTypeParam] TValue> : IReadOnlyByRefList<TValue> where TValue : struct, IEquatable<TValue>, IComparable<TValue>
    {
        /// <summary>
        /// Search the collection using the specified <paramref name="comparer"/> to determine if the collection contains
        /// the specified <paramref name="item"/>
        /// </summary>
        /// <typeparam name="TComparer">The type of the comparer to use.</typeparam>
        /// <param name="item">the item to find</param>
        /// <param name="comparer">the comparer to use</param>
        /// <returns>true if the item is found, false otherwise.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}"><paramref name="comparer"/>
        /// was not properly initialized and requires proper initialization to work.</exception>
        bool Contains<TComparer>(in TValue item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Search the collection using a default initialized <typeparamref name="TComparer"/>
        /// to determine whether the collection contains the specified <paramref name="item"/>.
        /// </summary>
        /// <typeparam name="TComparer">The type of the comparer to use. It should work properly when default initialized.</typeparam>
        /// <param name="item">the item to find</param>
        /// <returns>true if the item is found, false otherwise.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}"> <typeparamref name="TComparer"/> does not work properly when default-initialized</exception>
        bool Contains<TComparer>(in TValue item)
            where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Searches a section of the list for a given element using a binary search
        /// algorithm.  This method assumes that the given
        /// section of the list is already sorted; if this is not the case, the
        /// result will be incorrect.
        /// </summary>
        /// <typeparam name="TComparer">The type of the comparer to  use</typeparam>
        /// <param name="index">The zero-based starting index of the range to search.</param>
        /// <param name="count">The length of the range to search.</param>
        /// <param name="item">The object to locate. The value can be null for reference types.</param>
        /// <param name="comparer">The comparer.
        /// implementation to use when comparing elements, or null to use the default comparer Default.</param>
        /// <returns>  The method returns the index of the given value in the list. If the
        /// list does not contain the given value, the method returns a negative
        /// integer. The bitwise complement operator (~) can be applied to a
        /// negative result to produce the index of the first element (if any) that
        /// is larger than the given search value. This is also the index at which
        /// the search value should be inserted into the list in order for the list
        /// to remain sorted.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> or <paramref name="count"/> was negative.</exception>
        /// <exception cref="ArgumentException"><see cref="ByRefList{T}.Count"/> - <paramref name="index"/> is less than <paramref name="count"/></exception>
        /// <exception cref="BadComparerException{TComparer,TValue}">Supplied comparer was not initialized properly and comparers of type <typeparamref name="TComparer"/> do not work properly when not properly initialized.</exception>
        /// <remarks>Uses a utility optimized for comparisons of large value types.</remarks>
        int BinarySearch<TComparer>(int index, int count, in TValue item, TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Searches the list for a given element using a binary search
        /// algorithm.
        /// </summary>
        /// <param name="item">the item to find</param>
        /// <param name="comparer">the comparer</param>
        /// <returns> The method returns the index of the given value in the list. If the
        /// list does not contain the given value, the method returns a negative
        /// integer. The bitwise complement operator (~) can be applied to a
        /// negative result to produce the index of the first element (if any) that
        /// is larger than the given search value. This is also the index at which
        /// the search value should be inserted into the list in order for the list
        /// to remain sorted.</returns>
        int BinarySearch<TComparer>(in TValue item, in TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Searches the list for a given element using a binary search
        /// algorithm.  Default constructs comparer of type <typeparamref name="TComparer"/> to use for comparisons.
        /// </summary>
        /// <param name="item">the item to find</param>
        ///  <returns> The method returns the index of the given value in the list. If the
        /// list does not contain the given value, the method returns a negative
        /// integer. The bitwise complement operator (~) can be applied to a
        /// negative result to produce the index of the first element (if any) that
        /// is larger than the given search value. This is also the index at which
        /// the search value should be inserted into the list in order for the list
        /// to remain sorted.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}"><typeparamref name="TComparer"/> does not work properly when default initialized.</exception>
        int BinarySearch<TComparer>(in TValue item) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the first index of the specified item in the collection.
        /// </summary>
        /// <typeparam name="TComparer">The type of comparer</typeparam>
        /// <param name="item">the item whose index you want to find.</param>
        /// <param name="comparer">the comparer to use when finding the item</param>
        /// <returns>the index of occurence of <paramref name="item"/> in the collection or a negative number
        /// if not found.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized
        /// and has not been properly initialized.</exception>
        /// <remarks>May be faster especially for large value types.</remarks>
        int IndexOf<TComparer>(in TValue item, TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the first index of the specified item in a sub-range of this collection.
        /// Start looking with the item at <paramref name="startingIndex"/> and consider <paramref name="count"/> items.
        /// </summary>
        /// <typeparam name="TComparer">The comparer type</typeparam>
        /// <param name="item">the item you seek</param>
        /// <param name="startingIndex">the index of the first item to consider</param>
        /// <param name="count">the number of items to consider</param>
        /// <param name="comparer">the comparer to use</param>
        /// <returns>the index of the first occurence of <paramref name="item"/> in the sub-range described by <paramref name="startingIndex"/> and <paramref name="count"/>; a negative
        /// number if not found.</returns>
        /// <exception cref="ArgumentNegativeException"><paramref name="startingIndex"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startingIndex"/> is outside the bounds of the collection.</exception>
        /// <exception cref="ArgumentException"><paramref name="startingIndex"/> and <paramref name="count"/> when taken together, do not describe a valid sub-range
        /// of this collection.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        int IndexOf<TComparer>(in TValue item, int startingIndex, int count, TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the first index of the specified item in a sub-range of this collection.
        /// Start looking with the item at <paramref name="startingIndex"/> and consider <paramref name="count"/> items.
        /// </summary>
        /// <typeparam name="TComparer">The comparer type will be default constructed</typeparam>
        /// <param name="item">the item you seek</param>
        /// <param name="startingIndex">the index of the first item to consider</param>
        /// <param name="count">the number of items to consider</param>
        /// <returns>the index of the first occurence of <paramref name="item"/> in the sub-range described by <paramref name="startingIndex"/> and <paramref name="count"/>; a negative
        /// number if not found.</returns>
        /// <exception cref="ArgumentNegativeException"><paramref name="startingIndex"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startingIndex"/> is outside the bounds of the collection.</exception>
        /// <exception cref="ArgumentException"><paramref name="startingIndex"/> and <paramref name="count"/> when taken together, do not describe a valid sub-range
        /// of this collection.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works properly when default initialized.</exception>
        int IndexOf<TComparer>(in TValue item, int startingIndex, int count) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the last index of the specified item in a sub-range of this collection.
        /// </summary>
        /// <typeparam name="TComparer">The comparer type</typeparam>
        /// <param name="item">the item you seek</param>
        /// <param name="comparer">the comparer to use</param>
        /// <returns>the index of the last occurence of <paramref name="item"/> in collection; a negative
        /// number if not found.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        int LastIndexOf<TComparer>(in TValue item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the last index of the specified item in a sub-range of this collection.
        /// Start looking with the item at <paramref name="startingIndex"/> and work backwards considering <paramref name="count"/> items.
        /// </summary>
        /// <typeparam name="TComparer">The comparer type</typeparam>
        /// <param name="item">the item you seek</param>
        /// <param name="startingIndex">the index of the first item to consider</param>
        /// <param name="count">the number of items to consider</param>
        /// <param name="comparer">the comparer to use</param>
        /// <returns>the index of the last occurence of <paramref name="item"/> in the sub-range described by <paramref name="startingIndex"/> and <paramref name="count"/>; a negative
        /// number if not found.</returns>
        /// <exception cref="ArgumentNegativeException"><paramref name="startingIndex"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startingIndex"/> is outside the bounds of the collection.</exception>
        /// <exception cref="ArgumentException"><paramref name="startingIndex"/> and <paramref name="count"/> when taken together, do not describe a valid sub-range
        /// of this collection.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        int LastIndexOf<TComparer>(in TValue item, int startingIndex, int count, TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the last index of the specified item in a sub-range of this collection.
        /// Start looking with the item at <paramref name="startingIndex"/> and work backwards considering <paramref name="count"/> items.
        /// </summary>
        /// <typeparam name="TComparer">The comparer type</typeparam>
        /// <param name="item">the item you seek</param>
        /// <param name="startingIndex">the index of the first item to consider</param>
        /// <param name="count">the number of items to consider</param>
        /// <returns>the index of the last occurence of <paramref name="item"/> in the sub-range described by <paramref name="startingIndex"/> and <paramref name="count"/>; a negative
        /// number if not found.</returns>
        /// <exception cref="ArgumentNegativeException"><paramref name="startingIndex"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startingIndex"/> is outside the bounds of the collection.</exception>
        /// <exception cref="ArgumentException"><paramref name="startingIndex"/> and <paramref name="count"/> when taken together, do not describe a valid sub-range
        /// of this collection.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        int LastIndexOf<TComparer>(in TValue item, int startingIndex, int count) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the first index of the specified item in the collection.
        /// </summary>
        /// <typeparam name="TComparer">The type of comparer, which will be default initialized.</typeparam>
        /// <param name="item">the item whose index you want to find.</param>
        /// <returns>the index of occurence of <paramref name="item"/> in the collection or a negative number
        /// if not found.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized
        /// and has not been properly initialized.</exception>
        /// <remarks>May be faster especially for large value types.</remarks>
        int IndexOf<TComparer>(in TValue item) where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Find the last index of the specified item in the collection.
        /// </summary>
        /// <typeparam name="TComparer">The type of comparer, which will be default initialized.</typeparam>
        /// <param name="item">the item whose index you want to find.</param>
        /// <returns>the index of the last occurence of <paramref name="item"/> in the collection or a negative number
        /// if not found.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized
        /// and has not been properly initialized.</exception>
        /// <remarks>May be faster especially for large value types.</remarks>
        int LastIndexOf<TComparer>(in TValue item)
            where TComparer : struct, IByRefCompleteComparer<TValue>;

        /// <summary>
        /// Get subrange
        /// </summary>
        /// <param name="index">index of first item in range</param>
        /// <param name="count">count of items in range</param>
        /// <returns>a new list that contains <paramref name="count"/> items from this list starting with
        /// <paramref name="index"/></returns>
        /// <exception cref="ArgumentNegativeException"><paramref name="count"/> or <paramref name="index"/> was negative.</exception>
        /// <exception cref="ArgumentException">Taken together <paramref name="index"/> and <paramref name="count"/> do not describe a valid subrange
        /// of this list.</exception>
        BigValueList<TValue> GetRange(int index, int count);
    }
}