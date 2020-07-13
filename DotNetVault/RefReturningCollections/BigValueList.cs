using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using JetBrains.Annotations;

namespace DotNetVault.RefReturningCollections
{


    /// <summary>
    /// A list for large structs that are both equatable and comparable
    /// </summary>
    /// <typeparam name="TValue">The struct type.</typeparam>
    public sealed partial class BigValueList<[VaultSafeTypeParam] TValue> : ByRefList<TValue>, IBigValueList<TValue>
        where TValue : struct, IEquatable<TValue>, IComparable<TValue>
    {
        /// <inheritdoc />
        public BigValueList()
        {
        }

        /// <inheritdoc />
        public BigValueList(int capacity) : base(capacity)
        {
        }

        /// <inheritdoc />
        public BigValueList(IEnumerable<TValue> collection) : base(collection)
        {
        }

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
        public bool Contains<TComparer>(in TValue item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            TComparer copy = comparer;
            return FindFirstIndexOf(in item, 0, Count, ref copy) > -1;
        }

        /// <summary>
        /// Search the collection using a default initialized <typeparamref name="TComparer"/>
        /// to determine whether the collection contains the specified <paramref name="item"/>.
        /// </summary>
        /// <typeparam name="TComparer">The type of the comparer to use. It should work properly when default initialized.</typeparam>
        /// <param name="item">the item to find</param>
        /// <returns>true if the item is found, false otherwise.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}"> <typeparamref name="TComparer"/> does not work properly when default-initialized</exception>
        public bool Contains<TComparer>(in TValue item)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            TComparer comparer = new TComparer();
            return FindFirstIndexOf(item, 0, Count, ref comparer) > -1;
        }

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
        public int BinarySearch<TComparer>(int index, int count, in TValue item, TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            if (index < 0)
                throw new ArgumentNegativeException<int>(nameof(index), index);
            if (count < 0)
                throw new ArgumentNegativeException<int>(nameof(count), count);
            if (_size - index < count)
                throw new ArgumentException(
                    $"The size of this collection ({_size}) minus {nameof(index)} " +
                    $"(value: {index}) is less than {nameof(count)} (value: {count}).");
            if (!comparer.IsValid && !comparer.WorksCorrectlyWhenDefaultConstructed)
                throw new BadComparerException<TComparer, TValue>(comparer);
            Contract.Ensures(Contract.Result<int>() <= index + count);
            Contract.EndContractBlock();

            return _theUtil.BinarySearch(_items, index, count, in item, in comparer);
        }

        /// <summary>
        /// Get a read-only view of this list
        /// </summary>
        /// <returns>A read only view of this list.</returns>
        public BigValListReadOnlyView GetReadOnlyView() => new BigValListReadOnlyView(this);

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
        public int BinarySearch<TComparer>(in TValue item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            if (!comparer.IsValid && !comparer.WorksCorrectlyWhenDefaultConstructed)
                throw new BadComparerException<TComparer, TValue>(comparer);
            Contract.Ensures(Contract.Result<int>() <= Count);
            return _theUtil.BinarySearch(_items, 0, Count, in item, in comparer);
        }

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
        public int BinarySearch<TComparer>(in TValue item) where TComparer : struct, IByRefCompleteComparer<TValue> =>
            BinarySearch(in item, new TComparer());

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
        public int IndexOf<TComparer>(in TValue item, TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
            => FindFirstIndexOf(in item, 0, Count, ref comparer);

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
        public int IndexOf<TComparer>(in TValue item, int startingIndex, int count, TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
            => FindFirstIndexOf(in item, startingIndex, count, ref comparer);

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
        public int IndexOf<TComparer>(in TValue item, int startingIndex, int count)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            var comparer = new TComparer();
            return FindFirstIndexOf(in item, startingIndex, count, ref comparer);
        }

        /// <summary>
        /// Find the last index of the specified item in a sub-range of this collection.
        /// </summary>
        /// <typeparam name="TComparer">The comparer type</typeparam>
        /// <param name="item">the item you seek</param>
        /// <param name="comparer">the comparer to use</param>
        /// <returns>the index of the last occurence of <paramref name="item"/> in collection; a negative
        /// number if not found.</returns>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        public int LastIndexOf<TComparer>(in TValue item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            var copy = comparer;
            return FindLastIndexOf(in item, Count - 1, Count, ref copy);
        }

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
        public int LastIndexOf<TComparer>(in TValue item, int startingIndex, int count, TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
            => FindLastIndexOf(in item, startingIndex, count, ref comparer);

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
        public int LastIndexOf<TComparer>(in TValue item, int startingIndex, int count)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            var comparer = new TComparer();
            return FindLastIndexOf(in item, startingIndex, count, ref comparer);
        }

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
        public int IndexOf<TComparer>(in TValue item) where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            var comparer = new TComparer();
            return FindFirstIndexOf(in item, 0, Count, ref comparer);
        }

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
        public int LastIndexOf<TComparer>(in TValue item)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            var comparer = new TComparer();
            return Count > 0 ? FindLastIndexOf(in item, Count - 1, Count, ref comparer) : -1;
        }

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
        public BigValueList<TValue> GetRange(int index, int count)
        {
            if (index < 0)
                throw new ArgumentNegativeException<int>(nameof(index), index);
            if (count < 0)
                throw new ArgumentNegativeException<int>(nameof(count), count);
            if (_size - index < count)
                throw new ArgumentException(
                    $"The list's {nameof(Count)} " +
                    $"(value: {_size}) less parameter {nameof(index)} (value: {index}) " +
                    $"(value of difference: {_size - index}) must be greater than or equal " +
                    $"to parameter {nameof(count)} (value: {count}).");
            Contract.Ensures(Contract.Result<ByRefList<TValue>>() != null);
            Contract.EndContractBlock();

            BigValueList<TValue> list = new BigValueList<TValue>(count);
            Array.Copy(_items, index, list._items, 0, count);
            list._size = count;
            return list;
        }

        /// <summary>
        /// Sort the list using the specified comparer
        /// </summary>
        /// <param name="comparer">the comparer</param>
        /// <typeparam name="TComparer">The type of the comparer</typeparam>
        /// <exception cref="BadComparerException{TComparer,TValue}"><paramref name="comparer"/> was not initialized property and does not work
        /// when default constructed.</exception>
        public void Sort<TComparer>(in TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            if (!comparer.IsValid && !comparer.WorksCorrectlyWhenDefaultConstructed)
                throw new BadComparerException<TComparer, TValue>(comparer);
            _theUtil.Sort(_items, _size, in comparer);
        }

        /// <summary>
        /// Sort the list using the specified comparer after default constructing the specified comparer
        /// </summary>
        /// <typeparam name="TComparer">The comparer -- must be an unmanaged value type that works correctly when default constructed.</typeparam>
        /// <exception cref="Exception"><typeparamref name="TComparer"/> does not work correctly when default constructed.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}"> Comparers of type <typeparamref name="TComparer"/> do not work when default constructed.
        /// </exception>
        public void Sort<TComparer>() where TComparer : struct, IByRefCompleteComparer<TValue> => Sort(new TComparer());

        /// <exception cref="ArgumentNegativeException"><paramref name="startingIndex"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startingIndex"/> is outside the bounds of the collection.</exception>
        /// <exception cref="ArgumentException"><paramref name="startingIndex"/> and <paramref name="count"/> when taken together, do not describe a valid sub-range
        /// of this collection.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        private int FindFirstIndexOf<TComparer>(in TValue item, int startingIndex, int count, ref TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            if (startingIndex < 0) throw new ArgumentNegativeException<int>(nameof(startingIndex), startingIndex);
            if (count < 0) throw new ArgumentNegativeException<int>(nameof(count), count);
            if (Count == 0) return -1;
            if (startingIndex >= Count)
                throw new ArgumentOutOfRangeException(nameof(startingIndex), startingIndex,
                    @"Parameter must be less than size of collection.");
            if (startingIndex + count > Count)
                throw new ArgumentException(
                    "Starting index and count considered together do not describe a valid subrange of this collection.");
            if (!comparer.IsValid && !comparer.WorksCorrectlyWhenDefaultConstructed)
                throw new BadComparerException<TComparer, TValue>(comparer);
            int currentCount = 0;
            while (currentCount < count)
            {
                ref readonly TValue val = ref _items[startingIndex];
                if (comparer.Equals(in val, in item))
                    return startingIndex;
                ++startingIndex;
                ++currentCount;
            }

            return -1;
        }

        /// <exception cref="ArgumentNegativeException"><paramref name="startingIndex"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startingIndex"/> is outside the bounds of the collection.</exception>
        /// <exception cref="ArgumentException"><paramref name="startingIndex"/> and <paramref name="count"/> when taken together, do not describe a valid sub-range
        /// of this collection.</exception>
        /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        private int FindLastIndexOf<TComparer>(in TValue item, int startingIndex, int count, ref TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TValue>
        {
            if (startingIndex < 0) throw new ArgumentNegativeException<int>(nameof(startingIndex), startingIndex);
            if (count < 0) throw new ArgumentNegativeException<int>(nameof(count), count);
            if (Count == 0) return -1;
            if (startingIndex >= Count)
                throw new ArgumentOutOfRangeException(nameof(startingIndex), startingIndex,
                    @"Parameter must be less than size of collection.");
            if (startingIndex - count < -1)
                throw new ArgumentException(
                    "Starting index and count considered together do not describe a valid subrange of this collection.");
            if (!comparer.IsValid && !comparer.WorksCorrectlyWhenDefaultConstructed)
                throw new BadComparerException<TComparer, TValue>(comparer);
            int currentCount = 0;
            while (currentCount < count)
            {
                ref readonly TValue val = ref _items[startingIndex];
                if (comparer.Equals(in val, in item))
                    return startingIndex;
                --startingIndex;
                ++currentCount;
            }

            return -1;
        }

        private protected override string GetStringRepresentation() =>
            $"({typeof(BigValueList<TValue>).Name} - Count: {Count}";

        private readonly BigValTypeSortAndSearchUtil _theUtil = new BigValTypeSortAndSearchUtil();
    }

    #region Nested type defs
    partial class BigValueList<TValue>
    {
        /// <summary>
        /// A stack-only, read only view of the BigValueList.
        /// </summary>
        public readonly ref struct BigValListReadOnlyView
        {
            /// <summary>
            /// true if this readonly view has been initialized properly, false otherwise.
            /// </summary>
            public bool IsInitialized => _wrapped != null;

            /// <summary>
            /// The number of items of type TValue that this list has internally reserved
            /// memory to store.
            /// </summary>
            public int Capacity
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _wrapped.Capacity;
            }

            /// <summary>
            /// Read-only property describing how many elements are in the List.
            /// </summary> 
            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _wrapped.Count;
            }
            /// <summary>
            /// Returns the value present at the given index by reference
            /// </summary>
            /// <param name="index">The index</param>
            /// <returns>A reference to the value at the specified index.</returns>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than or equal
            /// to the size of the collection.</exception>
            public ref readonly TValue this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _wrapped[index];
            }

            /// <summary>
            /// Get a string representation of this object.
            /// </summary>
            /// <returns>A string representation</returns>
            public new string ToString() => $"{typeof(BigValListReadOnlyView).Name} -- Count: {_wrapped.Count}";

            internal BigValListReadOnlyView(BigValueList<TValue> wrapMe) =>
                _wrapped = wrapMe ?? throw new ArgumentNullException(nameof(wrapMe));

            /// <summary>
            /// Get an enumerator to enumerate the collection
            /// </summary>
            /// <returns>an enumerator</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator GetEnumerator() => _wrapped.GetEnumerator();
            /// <summary>
            /// Get an enumerator with a filter such that it enumerates only items of shich the predicate is true.
            /// </summary>
            /// <param name="filter">The predicate filter to apply while enumerating.</param>
            /// <returns>An enumerator that enumerates items that satisfy the filtering predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="filter"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WhereEnumerator GetFilteredEnumerator(RefPredicate<TValue> filter) => _wrapped.GetFilteredEnumerator(filter);
            /// <summary>
            /// Get an enumerator that enumerates the items in this list and applies a transformation to them,
            /// returning a value of the type specified by <typeparamref name="TTargetType"/> (which must be vault-safe).
            /// </summary>
            /// <typeparam name="TTargetType">vault safe type returned</typeparam>
            /// <param name="transformer">the transforming function</param>
            /// <returns>A transforming enumerator to enumerate the collection.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="transformer"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SelectEnumerator<TTargetType> GetTransformingEnumerator<[VaultSafeTypeParam] TTargetType>(RefFunc<TValue, TTargetType> transformer) => _wrapped.GetTransformingEnumerator(transformer);
            /// <summary>
            /// Get a filtered transforming enumerator that uses the supplied transformer function <paramref name="transformer"/> on those elements of which the supplied predicate <paramref name="filter"/> is true.
            /// </summary>
            /// <typeparam name="TTargetType">The return type, must be vault safe</typeparam>
            /// <param name="filter">The filter to select which items are enumerated</param>
            /// <param name="transformer">a transformer to produce the enumerated result</param>
            /// <returns>A filtering, transforming enumerator</returns>
            /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="transformer"/> were null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SelectWhereEnumerator<TTargetType> GetFilteredTransformingEnumerator<TTargetType>(RefPredicate<TValue> filter, RefFunc<TValue, TTargetType> transformer) => _wrapped.GetFilteredTransformingEnumerator(filter, transformer);
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int BinarySearch<TComparer>(int index, int count, in TValue item, in TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue> => _wrapped.BinarySearch(index, count, in item, comparer);
            /// <summary>
            /// Searches the list for a given element using a binary search
            /// algorithm. This method assumes that the given
            /// section of the list is already sorted; if this is not the case, the
            /// result will be incorrect.
            /// </summary>
            /// <typeparam name="TComparer">The type of the comparer to  use</typeparam>
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
            /// <exception cref="BadComparerException{TComparer,TValue}">Supplied comparer was not initialized properly and comparers of type <typeparamref name="TComparer"/> do not work properly when not properly initialized.</exception>
            /// <remarks>Uses a utility optimized for comparisons of large value types.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int BinarySearch<TComparer>(in TValue item, in TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TValue> => _wrapped.BinarySearch(in item, in comparer);
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BigValueList<TValue> GetRange(int index, int count) => _wrapped.GetRange(index, count);

            /// <summary>
            /// Query whether all elements in this collection satisfy the predicate supplied by
            /// <paramref name="predicate"/>
            /// </summary>
            /// <param name="predicate">The predicate</param>
            /// <returns>True if the collection is empty or if every element in the collection satisfied <paramref name="predicate"/></returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool All([NotNull] RefPredicate<TValue> predicate) => _wrapped.All(predicate);
            /// <summary>
            /// Query whether at least one item in the collection satisfies the supplied predicate.
            /// </summary>
            /// <param name="predicate">The predicate</param>
            /// <returns>True if at least one item in the collection satisfies the predicate, false otherwise.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Any([NotNull] RefPredicate<TValue> predicate) => _wrapped.Any(predicate);
            /// <summary>
            /// Return the first item in the collection that satisfies the given predicate.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The first item in the collection that satisfies <paramref name="predicate"/>.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">No item in the collection satisfied the predicate (or collection empty).</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue First([NotNull] RefPredicate<TValue> predicate) => ref _wrapped.First(predicate);
            /// <summary>
            /// Return the first element in the collection that satisfies the predicate or return the
            /// default value of TValue.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The first item that satisfied the predicate or the default value of TValue if no item satisfies the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue FirstOrDefault([NotNull] RefPredicate<TValue> predicate) => ref _wrapped.FirstOrDefault(predicate);
            /// <summary>
            /// Return the last element in the collection that satisfies the predicate.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The last item that satisfied the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">Collection contained no matching value.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue Last([NotNull] RefPredicate<TValue> predicate) => ref _wrapped.Last(predicate);
            /// <summary>
            /// Return the last element in the collection that satisfies the predicate or return the
            /// default value of TValue.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The last item that satisfied the predicate or the default value of TValue if no item satisfies the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue LastOrDefault([NotNull] RefPredicate<TValue> predicate) => ref _wrapped.LastOrDefault(predicate);
            /// <summary>
            /// Return the sole item in the collection that satisfies the predicate.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The only item in the collection that satisfies the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">The number of items in the collection that satisfy the collection was not EXACTLY ONE.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue Single([NotNull] RefPredicate<TValue> predicate) => ref _wrapped.Single(predicate);
            /// <summary>
            /// Return the sole item in the collection that satisfies the predicate or the default value of TValue if zero items
            /// in the collection satisfy it.
            /// </summary>
            /// <param name="predicate">The predicate</param>
            /// <returns>The sole item in the collection that satisfies the predicate, if any, otherwise the default value of TValue</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">More than one item in the collection satisfies the predicate.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue SingleOrDefault([NotNull] RefPredicate<TValue> predicate) => ref _wrapped.SingleOrDefault(predicate);
            /// <summary>
            /// Return the first element in the collection.
            /// </summary>
            /// <returns>The first item</returns>
            /// <exception cref="InvalidOperationException">The collection is empty.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue First() => ref _wrapped.First();
            /// <summary>
            /// Get the first item in the collection.
            /// </summary>
            /// <returns>The first item in the collection if any, otherwise default value of TValue </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue FirstOrDefault() => ref _wrapped.FirstOrDefault();
            /// <summary>
            /// Return the last element in the collection.
            /// </summary>
            /// <returns>The last item</returns>
            /// <exception cref="InvalidOperationException">The collection is empty.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue Last() => ref _wrapped.Last();
            /// <summary>
            /// Get the last item in the collection.
            /// </summary>
            /// <returns>The last item in the collection if any, otherwise default value of TValue </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue LastOrDefault() => ref _wrapped.LastOrDefault();
            /// <summary>
            /// Get the sole item in the collection.
            /// </summary>
            /// <returns>The sole item in the collection.</returns>
            /// <exception cref="InvalidOperationException">The collection did not contain EXACTLY one element.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue Single() => ref _wrapped.Single();
            /// <summary>
            /// Get the sole item in the collection or -- if empty -- the default value of TValue
            /// </summary>
            /// <returns>the sole item in the collection or -- if empty -- the default value of TValue</returns>
            /// <exception cref="InvalidOperationException">There was more than one element in the collection.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref readonly TValue SingleOrDefault() => ref _wrapped.SingleOrDefault();
            /// <summary>
            /// Search the collection using a default initialized <typeparamref name="TComparer"/>
            /// to determine whether the collection contains the specified <paramref name="item"/>.
            /// </summary>
            /// <typeparam name="TComparer">The type of the comparer to use. It should work properly when default initialized.</typeparam>
            /// <param name="item">the item to find</param>
            /// <returns>true if the item is found, false otherwise.</returns>
            /// <exception cref="BadComparerException{TComparer,TValue}"> <typeparamref name="TComparer"/> does not work properly when default-initialized</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains<TComparer>(in TValue item) where TComparer : struct, IByRefCompleteComparer<TValue> => _wrapped.Contains<TComparer>(in item);
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains<TComparer>(in TValue item, in TComparer comparer)
                where TComparer : struct, IByRefCompleteComparer<TValue> =>
                _wrapped.Contains(in item, in comparer);
            /// <summary>
            /// Find every item in this collection that satisfied the supplied predicate and return
            /// and immutable array of such items.
            /// </summary>
            /// <param name="match">The predicate.</param>
            /// <returns>An immutable array of all items in the collection that satisfy the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ImmutableArray<TValue> FindAll([NotNull] RefPredicate<TValue> match) => _wrapped.FindAll(match);
            /// <summary>
            /// Find the index of the first item in the collection that satisfies the predicate supplied
            /// by <paramref name="match"/>.
            /// </summary>
            /// <param name="match">The predicate to supply.</param>
            /// <returns>The index of the first item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindIndex([NotNull] RefPredicate<TValue> match) => _wrapped.FindIndex(match);
            /// <summary>
            /// Starting with index specified by <paramref name="startIndex"/>, find the index of the first item that matches the specified predicate.
            /// </summary>
            /// <param name="startIndex">the starting index</param>
            /// <param name="match">the predicate</param>
            /// <returns>The first index at or after <paramref name="startIndex"/> where the item satisfied <paramref name="match"/>; a negative number if no such element.</returns>
            ///  <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindIndex(int startIndex, [NotNull] RefPredicate<TValue> match) => _wrapped.FindIndex(startIndex, match);
            /// <summary>
            /// Searching the <paramref name="count"/> items starting with <paramref name="startIndex"/>, find the index of the first item
            /// that satisfies <paramref name="match"/>.
            /// </summary>
            /// <param name="startIndex">the start index</param>
            /// <param name="count">the number of items</param>
            /// <param name="match">the predicate</param>
            /// <returns>The index of the first item (within the <paramref name="count"/> items starting with <paramref name="startIndex"/>) that satisfies
            /// <paramref name="match"/>; a negative number if no such item.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
            /// <exception cref="ArgumentException">There are not <paramref name="count"/> items to search starting with <paramref name="startIndex"/> and working backwards.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindIndex(int startIndex, int count, [NotNull] RefPredicate<TValue> match) => _wrapped.FindIndex(startIndex, count, match);
            /// <summary>
            /// Find the index of the last item in the collection that satisfies the predicate supplied
            /// by <paramref name="match"/>.
            /// </summary>
            /// <param name="match">The predicate to supply.</param>
            /// <returns>The index of the last item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindLastIndex([NotNull] RefPredicate<TValue> match) => _wrapped.FindLastIndex(match);
            /// <summary>
            /// Find the index of the last item (starting with <paramref name="startIndex"/> and working backwards) that satisfies the predicate.
            /// </summary>
            /// <param name="startIndex">The index to begin from (working backwards).</param>
            /// <param name="match">The predicate</param>
            /// <returns>The index of the last item that satisfied <paramref name="match"/> or a negative number if no such.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindLastIndex(int startIndex, RefPredicate<TValue> match) => _wrapped.FindLastIndex(startIndex, match);
            /// <summary>
            /// Find the last index of the item that satisfies <paramref name="match"/> starting at the item at <paramref name="startIndex"/> and working back
            /// over <paramref name="count"/> items. 
            /// </summary>
            /// <param name="startIndex">The index to start with (working backwards).</param>
            /// <param name="count">The number of items to consider.</param>
            /// <param name="match"></param>
            /// <returns>The index of the last item or a negative number if no such.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
            /// <exception cref="ArgumentException">There are not <paramref name="count"/> items to search starting with <paramref name="startIndex"/> and working backwards.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindLastIndex(int startIndex, int count, RefPredicate<TValue> match) => _wrapped.FindLastIndex(startIndex, count, match);
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOf<TComparer>(in TValue item) where TComparer : struct, IByRefCompleteComparer<TValue> => _wrapped.IndexOf<TComparer>(in item);

            /// <summary>
            /// Searches the list for a given element using a binary search
            /// algorithm.  Uses <typeparamref name="TComparer"/> supplied by <paramref name="comparer"/>.
            /// </summary>
            /// <param name="item">the item to find</param>
            /// <param name="comparer">the comparer to use</param>
            /// <returns> The method returns the index of the given value in the list. If the
            /// list does not contain the given value, the method returns a negative
            /// integer. The bitwise complement operator (~) can be applied to a
            /// negative result to produce the index of the first element (if any) that
            /// is larger than the given search value. This is also the index at which
            /// the search value should be inserted into the list in order for the list
            /// to remain sorted.</returns>
            /// <exception cref="BadComparerException{TComparer,TValue}"><typeparamref name="TComparer"/>
            /// was not properly initialized and does not work properly unless so initialized.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOf<TComparer>(in TValue item, in TComparer comparer)
                where TComparer : struct, IByRefCompleteComparer<TValue> =>
                _wrapped.IndexOf(in item, comparer);
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOf<TComparer>(in TValue item, int startingIndex, int count)
                where TComparer : struct, IByRefCompleteComparer<TValue> =>
                _wrapped.IndexOf(in item, startingIndex, count, new TComparer());
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOf<TComparer>(in TValue item, int startingIndex, int count, in TComparer comparer)
                where TComparer : struct, IByRefCompleteComparer<TValue> =>
                _wrapped.IndexOf(in item, startingIndex, count, comparer);

            /// <summary>
            /// Find the last index of the specified item in a sub-range of this collection.
            /// </summary>
            /// <typeparam name="TComparer">The comparer type</typeparam>
            /// <param name="item">the item you seek</param>
            /// <param name="comparer">the comparer to use</param>
            /// <returns>the index of the last occurence of <paramref name="item"/> in collection; a negative
            /// number if not found.</returns>
            /// <exception cref="BadComparerException{TComparer,TValue}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int LastIndexOf<TComparer>(in TValue item, in TComparer comparer)
                where TComparer : struct, IByRefCompleteComparer<TValue> =>
                _wrapped.LastIndexOf(in item, comparer);

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int LastIndexOf<TComparer>(in TValue item)
                where TComparer : struct, IByRefCompleteComparer<TValue> => _wrapped.LastIndexOf<TComparer>(in item);

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int LastIndexOf<TComparer>(in TValue item, int startingIndex, int count)
                where TComparer : struct, IByRefCompleteComparer<TValue> =>
                _wrapped.LastIndexOf(in item, startingIndex, count, new TComparer());
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int LastIndexOf<TComparer>(in TValue item, int startingIndex, int count, in TComparer comparer)
                where TComparer : struct, IByRefCompleteComparer<TValue> =>
                _wrapped.LastIndexOf(in item, startingIndex, count, comparer);

            /// <summary>
            /// Copy the collection to an immutable array.
            /// </summary>
            /// <returns>An immutable array that contains a copy of all the elements herein.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ImmutableArray<TValue> ToArray() => _wrapped.ToArray();

            #region Privates
            private readonly BigValueList<TValue> _wrapped;
            #endregion
        }
    } 
    #endregion
}