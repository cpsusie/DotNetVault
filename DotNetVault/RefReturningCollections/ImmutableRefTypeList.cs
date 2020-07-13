using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace DotNetVault.RefReturningCollections
{
    /// <summary>
    /// A by ref list for immutable reference types
    /// </summary>
    /// <typeparam name="TImmutRef">Immutable reference type</typeparam>
    public sealed partial class ImmutableRefTypeList<[VaultSafeTypeParam] TImmutRef> : ByRefList<TImmutRef> where TImmutRef : class
    {
        /// <inheritdoc />
        public ImmutableRefTypeList()
        {
        }

        /// <inheritdoc />
        public ImmutableRefTypeList(int capacity) : base(capacity)
        {
        }

        /// <inheritdoc />
        public ImmutableRefTypeList(IEnumerable<TImmutRef> collection) : base(collection)
        {
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
        public ImmutableRefTypeList<TImmutRef> GetRange(int index, int count)
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
            Contract.Ensures(Contract.Result<ByRefList<TImmutRef>>() != null);
            Contract.EndContractBlock();

            ImmutableRefTypeList<TImmutRef> list = new ImmutableRefTypeList<TImmutRef>(count);
            Array.Copy(_items, index, list._items, 0, count);
            list._size = count;
            return list;
        }

        /// <summary>
        /// Get a read-only view of this list.
        /// </summary>
        /// <returns>A read only view</returns>
        public ImmutableRefListReadOnlyView GetReadOnlyView() => new ImmutableRefListReadOnlyView(this);

        private protected override string GetStringRepresentation() =>
            $"({typeof(ImmutableRefTypeList<TImmutRef>).Name} - Count: {Count}";
    }

    partial class ImmutableRefTypeList<TImmutRef>
    {
        /// <summary>
        /// A readonly view of a <see cref="ImmutableRefTypeList{TImmutRef}"/>
        /// </summary>
        public readonly ref struct ImmutableRefListReadOnlyView
        {
            /// <summary>
            /// true if this readonly view has been initialized properly, false otherwise.
            /// </summary>
            public bool IsInitialized
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _value != null;
            }

            /// <summary>
            /// Gets and sets the capacity of this list.The capacity is the size of 
            ///the internal array used to hold items.When set, the internal
            /// array of the list is reallocated to the given capacity.
            /// </summary>
            public int Capacity
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _value.Capacity;
            }

            /// <summary>
            /// Read-only property describing how many elements are in the List.
            /// </summary> 
            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _value.Count;
            }

            /// <summary>
            /// Returns the value present at the given index by readonly reference
            /// </summary>
            /// <param name="index">The index</param>
            /// <returns>A reference to the value at the specified index.</returns>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than or equal
            /// to the size of the collection.</exception>
            [CanBeNull]
            public ref readonly TImmutRef this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _value[index];
            }

            #region Public Methods
            /// <summary>
            /// Get an enumerator to enumerate the collection
            /// </summary>
            /// <returns>an enumerator</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator GetEnumerator() => _value.GetEnumerator();

            /// <summary>
            /// Get an enumerator with a filter such that it enumerates only items of shich the predicate is true.
            /// </summary>
            /// <param name="filter">The predicate filter to apply while enumerating.</param>
            /// <returns>An enumerator that enumerates items that satisfy the filtering predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="filter"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WhereEnumerator GetFilteredEnumerator(RefPredicate<TImmutRef> filter) => _value.GetFilteredEnumerator(filter);

            /// <summary>
            /// Get an enumerator that enumerates the items in this list and applies a transformation to them,
            /// returning a value of the type specified by <typeparamref name="TTargetType"/> (which must be vault-safe).
            /// </summary>
            /// <typeparam name="TTargetType">vault safe type returned</typeparam>
            /// <param name="transformer">the transforming function</param>
            /// <returns>A transforming enumerator to enumerate the collection.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="transformer"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SelectEnumerator<TTargetType> GetTransformingEnumerator<TTargetType>(RefFunc<TImmutRef, TTargetType> transformer) => _value.GetTransformingEnumerator(transformer);

            /// <summary>
            /// Get a filtered transforming enumerator that uses the supplied transformer function <paramref name="transformer"/> on those elements of which the supplied predicate <paramref name="filter"/> is true.
            /// </summary>
            /// <typeparam name="TTargetType">The return type, must be vault safe</typeparam>
            /// <param name="filter">The filter to select which items are enumerated</param>
            /// <param name="transformer">a transformer to produce the enumerated result</param>
            /// <returns>A filtering, transforming enumerator</returns>
            /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="transformer"/> were null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SelectWhereEnumerator<TTargetType> GetFilteredTransformingEnumerator<TTargetType>(RefPredicate<TImmutRef> filter, RefFunc<TImmutRef, TTargetType> transformer) => _value.GetFilteredTransformingEnumerator(filter, transformer);

            /// <summary>
            /// Query whether all elements in this collection satisfy the predicate supplied by
            /// <paramref name="predicate"/>
            /// </summary>
            /// <param name="predicate">The predicate</param>
            /// <returns>True if the collection is empty or if every element in the collection satisfied <paramref name="predicate"/></returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool All(RefPredicate<TImmutRef> predicate) => _value.All(predicate);

            /// <summary>
            /// Query whether at least one item in the collection satisfies the supplied predicate.
            /// </summary>
            /// <param name="predicate">The predicate</param>
            /// <returns>True if at least one item in the collection satisfies the predicate, false otherwise.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Any(RefPredicate<TImmutRef> predicate) => _value.Any(predicate);

            /// <summary>
            /// Return the first item in the collection that satisfies the given predicate.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The first item in the collection that satisfies <paramref name="predicate"/>.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">No item in the collection satisfied the predicate (or collection empty).</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef First(RefPredicate<TImmutRef> predicate) => ref _value.First(predicate);

            /// <summary>
            /// Return the first element in the collection that satisfies the predicate or return the
            /// default value of TImmutRef.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The first item that satisfied the predicate or null if no item satisfies the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef FirstOrDefault(RefPredicate<TImmutRef> predicate) => ref _value.FirstOrDefault(predicate);

            /// <summary>
            /// Return the last element in the collection that satisfies the predicate.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The last item that satisfied the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">Collection contained no matching value.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef Last(RefPredicate<TImmutRef> predicate) => ref _value.Last(predicate);

            /// <summary>
            /// Return the last element in the collection that satisfies the predicate or return null.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The last item that satisfied the predicate or null if no item satisfies the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef LastOrDefault(RefPredicate<TImmutRef> predicate) => ref _value.LastOrDefault(predicate);

            /// <summary>
            /// Return the single element in the collection that satisfies the predicate.
            /// </summary>
            /// <param name="predicate">the predicate</param>
            /// <returns>The single item in the collection that satisfies the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">The collection did not contain EXACTLY one item that satisfies <paramref name="predicate"/>.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef Single(RefPredicate<TImmutRef> predicate) => ref _value.Single(predicate);

            /// <summary>
            /// Return the single item in the collection that satisfied the predicate or, if there is no such item,
            /// return null.
            /// </summary>
            /// <param name="predicate">The predicate to be applied to the items in the collection.</param>
            /// <returns>The single item in the collection that satisfies <paramref name="predicate"/> or, if there is no such value,
            /// null.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
            /// <exception cref="InvalidOperationException">More than one item in the collection satisfied <paramref name="predicate"/>.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef SingleOrDefault(RefPredicate<TImmutRef> predicate) => ref _value.SingleOrDefault(predicate);

            /// <summary>
            /// Return the first item in the collection
            /// </summary>
            /// <returns>The first item in the collection.</returns>
            /// <exception cref="InvalidOperationException">The collection is empty.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef First() => ref _value.First();

            /// <summary>
            /// Return the first item in the collection or, if empty, return null.
            /// </summary>
            /// <returns>the first item in the collection or, if empty, null.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef FirstOrDefault() => ref _value.FirstOrDefault();

            /// <summary>
            /// Return the last element in the collection.
            /// </summary>
            /// <returns>The last element in the collection.</returns>
            /// <exception cref="InvalidOperationException">The collection was empty.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef Last() => ref _value.Last();

            /// <summary>
            /// Return the last item in the collection or, if empty, return null.
            /// </summary>
            /// <returns>the last item in the collection or, if empty, return null.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef LastOrDefault() => ref _value.LastOrDefault();

            /// <summary>
            /// Return the only item in the collection.
            /// </summary>
            /// <returns>The only item in the collection.</returns>
            /// <exception cref="InvalidOperationException">The collection does not contain EXACTLY one element.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef Single() => ref _value.Single();

            /// <summary>
            /// Return the only item in the collection, or, if the collection is empty, null.
            /// </summary>
            /// <returns>the only item in the collection, or, if the collection is empty, null.</returns>
            /// <exception cref="InvalidOperationException">The collection contains more than one element.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [CanBeNull] public ref readonly TImmutRef SingleOrDefault() => ref _value.SingleOrDefault();

            /// <summary>
            /// Searches a section of the list for a given element using a binary search
            /// algorithm. Elements of the list are compared to the search value using
            /// the given IComparer interface. If comparer is null, elements of
            /// the list are compared to the search value using the IComparable
            /// interface, which in that case must be implemented by all elements of the
            /// list and the given search value. This method assumes that the given
            /// section of the list is already sorted; if this is not the case, the
            /// result will be incorrect.
            /// </summary>
            /// <param name="index">The zero-based starting index of the range to search.</param>
            /// <param name="count">The length of the range to search.</param>
            /// <param name="item">The object to locate. The value can be null for reference types.</param>
            /// <param name="comparer">The <see cref="IComparer{T}"/>
            /// implementation to use when comparing elements, or null to use the default comparer Default.</param>
            /// <returns>  The method returns the index of the given value in the list. If the
            /// list does not contain the given value, the method returns a negative
            /// integer. The bitwise complement operator (~) can be applied to a
            /// negative result to produce the index of the first element (if any) that
            /// is larger than the given search value. This is also the index at which
            /// the search value should be inserted into the list in order for the list
            /// to remain sorted.</returns>
            /// <remarks> The method uses the Array.BinarySearch method to perform the
            /// search.</remarks>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> or <paramref name="count"/> was negative.</exception>
            /// <exception cref="ArgumentException"><see cref="ByRefList{T}.Count"/> - <paramref name="index"/> is less than <paramref name="count"/></exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int BinarySearch(int index, int count, in TImmutRef item, IComparer<TImmutRef> comparer) => _value.BinarySearch(index, count, in item, comparer);

            /// <summary>
            /// Searches the list for a given element using a binary search
            /// algorithm.
            /// </summary>
            /// <param name="item">the item to find</param>
            /// <returns> The method returns the index of the given value in the list. If the
            /// list does not contain the given value, the method returns a negative
            /// integer. The bitwise complement operator (~) can be applied to a
            /// negative result to produce the index of the first element (if any) that
            /// is larger than the given search value. This is also the index at which
            /// the search value should be inserted into the list in order for the list
            /// to remain sorted.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int BinarySearch(TImmutRef item) => _value.BinarySearch(item);

            /// <summary>
            /// Searches the list for a given element using a binary search
            /// algorithm.
            /// </summary>
            /// <param name="comparer">The comparer to use, if null, default comparer.
            /// will be used.</param>
            /// <param name="item">the item to find</param>
            /// <returns> The method returns the index of the given value in the list. If the
            /// list does not contain the given value, the method returns a negative
            /// integer. The bitwise complement operator (~) can be applied to a
            /// negative result to produce the index of the first element (if any) that
            /// is larger than the given search value. This is also the index at which
            /// the search value should be inserted into the list in order for the list
            /// to remain sorted.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int BinarySearch(TImmutRef item, IComparer<TImmutRef> comparer) => _value.BinarySearch(item, comparer);

            /// <summary>
            /// Contains returns true if the specified element is in the List.
            /// It does a linear, O(n) search.  Equality is determined by calling
            /// item.Equals().
            ///</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(in TImmutRef item) => _value.Contains(in item);

            /// <summary>
            /// Determines whether the this <see cref="ByRefList{T}"/> contains any
            /// elements that match the conditions defined by the specified predicate.
            /// </summary>
            /// <param name="match">the predicate</param>
            /// <returns>True if there are any elements that match the predicate, false otherwise.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Exists(RefPredicate<TImmutRef> match) => _value.Exists(match);

            /// <summary>
            /// Find every item in this collection that satisfied the supplied predicate and return
            /// and immutable array of such items.
            /// </summary>
            /// <param name="match">The predicate.</param>
            /// <returns>An immutable array of all items in the collection that satisfy the predicate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ImmutableArray<TImmutRef> FindAll(RefPredicate<TImmutRef> match) => _value.FindAll(match);

            /// <summary>
            /// Find the index of the first item in the collection that satisfies the predicate supplied
            /// by <paramref name="match"/>.
            /// </summary>
            /// <param name="match">The predicate to supply.</param>
            /// <returns>The index of the first item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindIndex(RefPredicate<TImmutRef> match) => _value.FindIndex(match);

            /// <summary>
            /// Starting with index specified by <paramref name="startIndex"/>, find the index of the first item that matches the specified predicate.
            /// </summary>
            /// <param name="startIndex">the starting index</param>
            /// <param name="match">the predicate</param>
            /// <returns>The first index at or after <paramref name="startIndex"/> where the item satisfied <paramref name="match"/>; a negative number if no such element.</returns>
            ///  <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindIndex(int startIndex, RefPredicate<TImmutRef> match) => _value.FindIndex(startIndex, match);

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
            public int FindIndex(int startIndex, int count, RefPredicate<TImmutRef> match) => _value.FindIndex(startIndex, count, match);

            /// <summary>
            /// Find the index of the last item in the collection that satisfies the predicate supplied
            /// by <paramref name="match"/>.
            /// </summary>
            /// <param name="match">The predicate to supply.</param>
            /// <returns>The index of the last item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindLastIndex(RefPredicate<TImmutRef> match) => _value.FindLastIndex(match);

            /// <summary>
            /// Find the index of the last item (starting with <paramref name="startIndex"/> and working backwards) that satisfies the predicate.
            /// </summary>
            /// <param name="startIndex">The index to begin from (working backwards).</param>
            /// <param name="match">The predicate</param>
            /// <returns>The index of the last item that satisfied <paramref name="match"/> or a negative number if no such.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindLastIndex(int startIndex, RefPredicate<TImmutRef> match) => _value.FindLastIndex(startIndex, match);

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
            public int FindLastIndex(int startIndex, int count, RefPredicate<TImmutRef> match) => _value.FindLastIndex(startIndex, count, match);

            /// <summary> Returns the index of the first occurrence of a given value in a range of
            ///this list. The list is searched forwards from beginning to end.
            ///The elements of the list are compared to the given value using the
            ///Object.Equals method.
            /// </summary>
            /// <returns>The index of the item or a negative number if not found.</returns>
            /// <remarks> This method uses the Array.IndexOf method to perform the
            /// search.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOf(in TImmutRef item) => _value.IndexOf(in item);

            /// <summary>
            /// Returns the index of the first occurrence of a given value in a range of
            /// this list. The list is searched forwards, starting at index
            /// specified by <paramref name="index"/>and ending at the end of the collection. The
            /// elements of the list are compared to the given value using the
            /// Object.Equals method.
            /// </summary>
            /// <returns>The index of the item if found, otherwise a negative number.</returns>
            /// <remarks>
            /// This method uses the Array.IndexOf method to perform the
            /// search.</remarks>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was greater than or equal to the size of the
            /// collection.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOf(in TImmutRef item, int index) => _value.IndexOf(in item, index);

            /// <summary>Returns the index of the first occurrence of a given value in a range of
            /// this list. The list is searched forwards, starting at index
            /// index and up to count number of elements. The
            /// elements of the list are compared to the given value using the
            /// Object.Equals method.</summary>
            /// 
            /// <returns>The index of the item if found, otherwise a negative number.</returns>
            /// <remarks>
            /// This method uses the Array.IndexOf method to perform the
            /// search.</remarks>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was greater than or equal to the size of the
            /// collection.</exception>
            /// <exception cref="ArgumentNegativeException{T}"><paramref name="count"/> was negative.</exception>
            /// <exception cref="ArgumentException"><paramref name="index"/> greater than size of collection - <paramref name="count"/>.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int IndexOf(in TImmutRef item, int index, int count) => _value.IndexOf(in item, index, count);

            /// <summary> Returns the index of the last occurrence of a given value in a range of
            /// this list. The list is searched backwards, starting at the end 
            /// and ending at the first element in the list. The elements of the list 
            /// are compared to the given value using the Object.Equals method. </summary>
            /// <remarks> This method uses the Array.LastIndexOf method to perform the
            /// search. </remarks>
            /// <returns>The index of the last occurence of item if found, a negative number otherwise.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int LastIndexOf(in TImmutRef item) => _value.LastIndexOf(in item);

            /// <summary>Returns the index of the last occurrence of a given value in a range of
            /// this list. The list is searched backwards, starting at index
            /// <paramref name="index"/>. The
            /// elements of the list are compared to the given value using the
            /// Object.Equals method.</summary>
            /// 
            /// <returns>The index of the item if found, otherwise a negative number.</returns>
            /// <remarks>
            /// This method uses the Array.LastIndexOf method to perform the
            /// search.</remarks>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was greater than or equal to the size of the
            /// collection.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int LastIndexOf(in TImmutRef item, int index) => _value.LastIndexOf(in item, index);

            /// <summary>Returns the index of the last occurrence of a given value in a range of
            /// this list. The list is searched backwards, starting at index
            /// <paramref name="index"/> and up to <paramref name="count"/> number of elements. The
            /// elements of the list are compared to the given value using the
            /// Object.Equals method.</summary>
            /// 
            /// <returns>The index of the item if found, otherwise a negative number.</returns>
            /// <remarks>
            /// This method uses the Array.LastIndexOf method to perform the
            /// search.</remarks>
            /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was greater than or equal to the size of the
            /// collection.</exception>
            /// <exception cref="ArgumentNegativeException{T}"><paramref name="count"/> was negative.</exception>
            /// <exception cref="ArgumentException"><paramref name="index"/> greater than size of collection - <paramref name="count"/>.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int LastIndexOf(in TImmutRef item, int index, int count) => _value.LastIndexOf(in item, index, count);

            /// <summary> ToArray returns a new immutable array containing the contents of the List. </summary>
            /// <remarks> This requires copying the List, which is an O(n) operation.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ImmutableArray<TImmutRef> ToArray() => _value.ToArray(); 
            #endregion

            #region CTOR
            internal ImmutableRefListReadOnlyView([NotNull] ImmutableRefTypeList<TImmutRef> value) =>
                _value = value ?? throw new ArgumentNullException(nameof(value)); 
            #endregion


            #region Privates
            private readonly ImmutableRefTypeList<TImmutRef> _value; 
            #endregion
        }
    }

}