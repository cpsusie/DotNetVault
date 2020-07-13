using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.RefReturningCollections
{
    /// <summary>
    /// Based on Microsoft's implementation of <see cref="System.Collections.Generic.List{T}"/>
    /// but requiring a vault safe type parameter and permitting return by reference, by reference comparisons, etc.
    /// </summary>
    /// <typeparam name="T">The stored type.</typeparam>
    /// <remarks>Use <see cref="BigValueList{TValue}"/> for value types and <see cref="ImmutableRefTypeList{TImmutRef}"/> for immutable reference types.</remarks>
    public abstract partial class ByRefList<[VaultSafeTypeParam] T> : IByRefList<T>
    {
        #region Properties and Indexer

        /// <summary>
        /// Gets and sets the capacity of this list.The capacity is the size of 
        ///the internal array used to hold items.When set, the internal
        /// array of the list is reallocated to the given capacity.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> was less than the
        /// size of the list.</exception>
        public int Capacity
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return _items.Length;
            }
            set
            {
                if (value < _size)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        @$"Parameter may not be less than the current size of the list ({_size}).");
                }
                Contract.EndContractBlock();

                if (value != _items.Length)
                {
                    if (value > 0)
                    {
                        T[] newItems = new T[value];
                        if (_size > 0)
                        {
                            Array.Copy(_items, 0, newItems, 0, _size);
                        }
                        _items = newItems;
                    }
                    else
                    {
                        _items = TheEmptyArray;
                    }
                }
            }
        }

        /// <summary>
        /// Read-only property describing how many elements are in the List.
        /// </summary> 
        public int Count
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return _size;
            }
        }

        /// <summary>
        /// Returns the value present at the given index by reference
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>A reference to the value at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than or equal
        /// to the size of the collection.</exception>
        public ref T this[int index]
        {
            get
            {
                // Following trick can reduce the range check by one
                if ((uint)index >= (uint)_size)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index,
                        @$"Parameter must be non-negative and less than the size of the list ([{_size.ToString()}]).");
                }
                Contract.EndContractBlock();
                return ref _items[index];
            }
        }

        
        /// <inheritdoc />
        ref readonly T IReadOnlyByRefList<T>.this[int index] => ref this[index]; 
        #endregion
        
        #region CTORS
        /// <summary>
        ///Constructs a List. The list is initially empty and has a capacity
        /// of zero. Upon adding the first element to the list the capacity is
        /// increased to 16, and then increased in multiples of two as required.
        /// </summary>
        private protected ByRefList() => _items = TheEmptyArray;


        /// <summary>
        /// Constructs a List with a given initial capacity. The list is
        /// initially empty, but will have room for the given number of elements
        /// before any reallocations are required.
        /// </summary>
        /// <param name="capacity">the starting capacity of the list</param>
        /// <exception cref="ArgumentOutOfRangeException">The initial capcity cannot be negative.</exception>
        private protected ByRefList(int capacity)
        {
            if (capacity < 0) throw new ArgumentNegativeException<int>(nameof(capacity), capacity);
            Contract.EndContractBlock();

            _items = capacity == 0 ? TheEmptyArray : new T[capacity];
        }


        /// <summary>
        /// Constructs a List, copying the contents of the given collection. The
        /// size and capacity of the new list will both be equal to the size of the
        /// given collection.
        /// </summary>
        /// <param name="collection">The collection</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        private protected ByRefList(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            Contract.EndContractBlock();

            ICollection<T> c = collection as ICollection<T>;
            if (c != null)
            {
                int count = c.Count;
                if (count == 0)
                {
                    _items = TheEmptyArray;
                }
                else
                {
                    _items = new T[count];
                    c.CopyTo(_items, 0);
                    _size = count;
                }
            }
            else
            {
                _size = 0;
                _items = TheEmptyArray;
                // This enumerable could be empty.  Let Add allocate a new array, if needed.
                // Note it will also go to _defaultCapacity first, not 1, then 2, etc.

                using (IEnumerator<T> en = collection.GetEnumerator())
                {
                    while (en.MoveNext())
                    {
                        Add(en.Current);
                    }
                }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get the string representation of this object.
        /// </summary>
        /// <returns>string representation</returns>
        [NotNull] public override string ToString() => GetStringRepresentation();

        /// <summary>
        /// Get an enumerator to enumerate the collection
        /// </summary>
        /// <returns>an enumerator</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Get an enumerator with a filter such that it enumerates only items of shich the predicate is true.
        /// </summary>
        /// <param name="filter">The predicate filter to apply while enumerating.</param>
        /// <returns>An enumerator that enumerates items that satisfy the filtering predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filter"/> was null.</exception>
        public WhereEnumerator GetFilteredEnumerator(RefPredicate<T> filter) => new WhereEnumerator(this, filter);

        /// <summary>
        /// Get an enumerator that enumerates the items in this list and applies a transformation to them,
        /// returning a value of the type specified by <typeparamref name="TTargetType"/> (which must be vault-safe).
        /// </summary>
        /// <typeparam name="TTargetType">vault safe type returned</typeparam>
        /// <param name="transformer">the transforming function</param>
        /// <returns>A transforming enumerator to enumerate the collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transformer"/> was null.</exception>
        public SelectEnumerator<TTargetType>
            GetTransformingEnumerator<[VaultSafeTypeParam] TTargetType>(
                RefFunc<T, TTargetType> transformer) => new SelectEnumerator<TTargetType>(this, transformer);

        /// <summary>
        /// Get a filtered transforming enumerator that uses the supplied transformer function <paramref name="transformer"/> on those elements of which the supplied predicate <paramref name="filter"/> is true.
        /// </summary>
        /// <typeparam name="TTargetType">The return type, must be vault safe</typeparam>
        /// <param name="filter">The filter to select which items are enumerated</param>
        /// <param name="transformer">a transformer to produce the enumerated result</param>
        /// <returns>A filtering, transforming enumerator</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="transformer"/> were null.</exception>
        public SelectWhereEnumerator<TTargetType> GetFilteredTransformingEnumerator<[VaultSafeTypeParam] TTargetType>(
            RefPredicate<T> filter, RefFunc<T, TTargetType> transformer) =>
            new SelectWhereEnumerator<TTargetType>(this, filter, transformer);

        /// <summary>
        /// Query whether all elements in this collection satisfy the predicate supplied by
        /// <paramref name="predicate"/>
        /// </summary>
        /// <param name="predicate">The predicate</param>
        /// <returns>True if the collection is empty or if every element in the collection satisfied <paramref name="predicate"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        public bool All(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!predicate(in enumerator.Current))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Query whether at least one item in the collection satisfies the supplied predicate.
        /// </summary>
        /// <param name="predicate">The predicate</param>
        /// <returns>True if at least one item in the collection satisfies the predicate, false otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        public bool Any(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (predicate(in enumerator.Current))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Return the first item in the collection that satisfies the given predicate.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The first item in the collection that satisfies <paramref name="predicate"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">No item in the collection satisfied the predicate (or collection empty).</exception>
        public ref readonly T First(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (predicate(in enumerator.Current))
                    return ref enumerator.Current;
            }

            throw new InvalidOperationException("Sequence contains no elements that satisfy the supplied predicate.");
        }

        /// <summary>
        /// Return the first element in the collection that satisfies the predicate or return the
        /// default value of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The first item that satisfied the predicate or the default value of <typeparamref name="T"/> if no item satisfies the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        public ref readonly T FirstOrDefault(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (predicate(in enumerator.Current))
                    return ref enumerator.Current;
            }
            return ref TheDefaultValue;
        }

        /// <summary>
        /// Return the last element in the collection that satisfies the predicate.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The last item that satisfied the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">Collection contained no matching value.</exception>
        public ref readonly T Last(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            for (int i = Count - 1; i > -1; --i)
            {
                ref readonly T val = ref _items[i];
                if (predicate(in val))
                    return ref val;
            }
            throw new InvalidOperationException("Sequence contains no matching elements.");
        }
        /// <summary>
        /// Return the last element in the collection that satisfies the predicate or return the
        /// default value of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The last item that satisfied the predicate or the default value of <typeparamref name="T"/> if no item satisfies the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        public ref readonly T LastOrDefault(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            for (int i = Count - 1; i > -1; --i)
            {
                ref readonly T val = ref _items[i];
                if (predicate(in val))
                    return ref val;
            }
            return ref TheDefaultValue;
        }

        /// <summary>
        /// Return the single element in the collection that satisfies the predicate.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The single item in the collection that satisfies the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">The collection did not contain EXACTLY one item that satisfies <paramref name="predicate"/>.</exception>
        public ref readonly T Single(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var enumerator = GetEnumerator();
            int matchCount = 0;
            ref readonly T match = ref TheDefaultValue;
            bool foundIt = false;
            while (enumerator.MoveNext())
            {
                if (predicate(in enumerator.Current))
                {
                    if (++matchCount > 1)
                        throw new InvalidOperationException(
                            "Sequence contains more than one element satisfying the predicate.");
                    match = ref enumerator.Current;
                    foundIt = true;
                }
            }

            if (foundIt)
            {
                return ref match;
            }
            throw new InvalidOperationException("Sequence contains no matching elements.");
        }

        /// <summary>
        /// Return the single item in the collection that satisfied the predicate or, if there is no such item,
        /// return the default value of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="predicate">The predicate to be applied to the items in the collection.</param>
        /// <returns>The single item in the collection that satisfies <paramref name="predicate"/> or, if there is no such value,
        /// the default value of <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">More than one item in the collection satisfied <paramref name="predicate"/>.</exception>
        public ref readonly T SingleOrDefault(RefPredicate<T> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var enumerator = GetEnumerator();
            int matchCount = 0;
            ref readonly T match = ref TheDefaultValue;
            while (enumerator.MoveNext())
            {
                if (predicate(in enumerator.Current))
                {
                    if (++matchCount > 1)
                        throw new InvalidOperationException(
                            "Sequence contains more than one element satisfying the predicate.");
                    match = ref enumerator.Current;
                }
            }
            Debug.Assert(matchCount == 1 || matchCount == 0);
            return ref match;
        }

        /// <summary>
        /// Return the first item in the collection
        /// </summary>
        /// <returns>The first item in the collection.</returns>
        /// <exception cref="InvalidOperationException">The collection is empty.</exception>
        public ref readonly T First()
        {
            switch (_size)
            {
                case 0:
                    throw new InvalidOperationException("Sequence is empty.");
                default:
                    return ref _items[0];
            }
        }

        /// <summary>
        /// Return the first item in the collection or, if empty, return the default value of <typeparamref name="T"/>.
        /// </summary>
        /// <returns>the first item in the collection or, if empty, return the default value of <typeparamref name="T"/>.</returns>
        public ref readonly T FirstOrDefault()
        {
            switch (_size)
            {
                case 0:
                    return ref TheDefaultValue;
                default:
                    return ref _items[0];
            }
        }

        /// <summary>
        /// Return the last element in the collection.
        /// </summary>
        /// <returns>The last element in the collection.</returns>
        /// <exception cref="InvalidOperationException">The collection was empty.</exception>
        public ref readonly T Last()
        {
            switch (_size)
            {
                case 0:
                    throw new InvalidOperationException("Sequence is empty.");
                default:
                    return ref _items[_size - 1];
            }
        }

        /// <summary>
        /// Return the last item in the collection or, if empty, return the default value of <typeparamref name="T"/>.
        /// </summary>
        /// <returns>the last item in the collection or, if empty, return the default value of <typeparamref name="T"/>.</returns>
        public ref readonly T LastOrDefault()
        {
            switch (_size)
            {
                case 0:
                    return ref TheDefaultValue;
                default:
                    return ref _items[_size - 1];
            }
        }

        /// <summary>
        /// Return the only item in the collection.
        /// </summary>
        /// <returns>The only item in the collection.</returns>
        /// <exception cref="InvalidOperationException">The collection does not contain EXACTLY one element.</exception>
        public ref readonly T Single()
        {
            switch (_size)
            {
                case 0:
                    throw new InvalidOperationException("Sequence is empty.");
                case 1:
                    return ref _items[0];
                default:
                    throw new InvalidOperationException("Sequence contains more than one element.");
            }
        }

        /// <summary>
        /// Return the only item in the collection, or, if the collection is empty, the default value of <typeparamref name="T"/>.
        /// </summary>
        /// <returns>the only item in the collection, or, if the collection is empty, the default value of <typeparamref name="T"/>.</returns>
        /// <exception cref="InvalidOperationException">The collection contains more than one element.</exception>
        public ref readonly T SingleOrDefault()
        {
            switch (_size)
            {
                case 0:
                    return ref TheDefaultValue;
                case 1:
                    return ref _items[0];
                default:
                    throw new InvalidOperationException("Sequence contains more than one element.");
            }
        }



        ///<summary>
        /// Adds the given object to the end of this list. The size of the list is
        /// increased by one. If required, the capacity of the list is doubled
        /// before adding the new element.
        ///</summary> 
        public void Add(in T item)
        {
            if (_size == _items.Length) EnsureCapacity(_size + 1);
            _items[_size++] = item;
        }


        /// <summary>Adds the elements of the given collection to the end of this list. If
        ///required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        public void AddRange(VsEnumerableWrapper<T> collection)
        {
            Contract.Ensures(Count >= Contract.OldValue(Count));
            InsertRange(_size, collection);
        }

        /// <summary>Adds the elements of the given collection to the end of this list. If
        ///required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        public void AddRange(VsListWrapper<T> collection)
        {
            Contract.Ensures(Count >= Contract.OldValue(Count));
            InsertRange(_size, collection);
        }

        /// <summary>Adds the elements of the given collection to the end of this list. If
        ///required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        public void AddRange(VsArrayWrapper<T> collection)
        {
            Contract.Ensures(Count >= Contract.OldValue(Count));
            InsertRange(_size, collection);
        }


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
        /// <exception cref="ArgumentException"><see cref="Count"/> - <paramref name="index"/> is less than <paramref name="count"/></exception>
        /// <remarks>If <typeparamref name="T"/> is a large value type, prefer the specialized overloads provided by <see cref="BigValueList{TValue}"/></remarks>
        public int BinarySearch(int index, int count, in T item, IComparer<T> comparer)
        {
            if (index < 0)
                throw new ArgumentNegativeException<int>(nameof(index), index);
            if (count < 0)
                throw new ArgumentNegativeException<int>(nameof(count), count);
            if (_size - index < count)
                throw new ArgumentException(
                    $"The size of this collection ({_size}) minus {nameof(index)} " +
                    $"(value: {index}) is less than {nameof(count)} (value: {count}).");
            Contract.Ensures(Contract.Result<int>() <= index + count);
            Contract.EndContractBlock();

            return Array.BinarySearch(_items, index, count, item, comparer);
        }

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
        /// <remarks>If <typeparamref name="T"/> is a large value type, prefer the specialized overloads provided by <see cref="BigValueList{TValue}"/></remarks>
        public int BinarySearch(T item)
        {
            Contract.Ensures(Contract.Result<int>() <= Count);
            return BinarySearch(0, Count, item, null);
        }
        /// <summary>
        /// Searches the list for a given element using a binary search
        /// algorithm.
        /// </summary>
        /// <param name="comparer">The comparer to use, if null, default comparer for <typeparamref name="T"/>
        /// will be used.</param>
        /// <param name="item">the item to find</param>
        /// <returns> The method returns the index of the given value in the list. If the
        /// list does not contain the given value, the method returns a negative
        /// integer. The bitwise complement operator (~) can be applied to a
        /// negative result to produce the index of the first element (if any) that
        /// is larger than the given search value. This is also the index at which
        /// the search value should be inserted into the list in order for the list
        /// to remain sorted.</returns>
        /// <remarks>If <typeparamref name="T"/> is a large value type, prefer the specialized overloads provided by<see cref="BigValueList{TValue}"/></remarks>
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            Contract.Ensures(Contract.Result<int>() <= Count);
            return BinarySearch(0, Count, item, comparer);
        }


        ///<summary>Clears the contents of List. </summary>
        public void Clear()
        {
            if (_size > 0)
            {
                Array.Clear(_items, 0, _size); // Don't need to doc this but we clear the elements so that the gc can reclaim the references.
                _size = 0;
            }
        }

        /// <summary>
        /// Contains returns true if the specified element is in the List.
        /// It does a linear, O(n) search.  Equality is determined by calling
        /// item.Equals().
        ///</summary> 
        public bool Contains(in T item)
        {
            if (ReferenceEquals(item, null))
            {
                for (int i = 0; i < _size; i++)
                    if (ReferenceEquals(_items[i], null))
                        return true;
                return false;
            }
            else
            {
                EqualityComparer<T> c = EqualityComparer<T>.Default;
                for (int i = 0; i < _size; i++)
                {
                    if (c.Equals(_items[i], item)) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Determines whether the this <see cref="ByRefList{T}"/> contains any
        /// elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">the predicate</param>
        /// <returns>True if there are any elements that match the predicate, false otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        public bool Exists(RefPredicate<T> match) => FindIndex(match) != -1;

        /// <summary>
        /// Search the list to see if any item matches the predicate and if so, return the
        /// first item that does.
        /// </summary>
        /// <param name="match">the predicate</param>
        /// <returns>A tuple whose FoundAny member indicates whether such an item was found.  If FoundAny
        /// is true, Value represents the found value, otherwise value will be default.</returns>
        public (bool FoundAny, T Value) Find(RefPredicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));

            Contract.EndContractBlock();

            for (int i = 0; i < _size; i++)
            {
                ref readonly T val = ref _items[i];
                if (match(in val))
                {
                    return (true, val);
                }
            }
            return (false, default);
        }

        /// <summary>
        /// Find every item in this collection that satisfied the supplied predicate and return
        /// and immutable array of such items.
        /// </summary>
        /// <param name="match">The predicate.</param>
        /// <returns>An immutable array of all items in the collection that satisfy the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        public ImmutableArray<T> FindAll(RefPredicate<T>
            match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));

            Contract.EndContractBlock();
            var bldr = ImmutableArray.CreateBuilder<T>();
            for (int i = 0; i < _size; i++)
            {
                ref readonly T val = ref _items[i];
                if (match(in val))
                {
                    bldr.Add(val);
                }
            }
            return bldr.Count == bldr.Capacity ? bldr.MoveToImmutable() : bldr.ToImmutable();
        }

        /// <summary>
        /// Find the index of the first item in the collection that satisfies the predicate supplied
        /// by <paramref name="match"/>.
        /// </summary>
        /// <param name="match">The predicate to supply.</param>
        /// <returns>The index of the first item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        public int FindIndex(RefPredicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            return FindIndex(0, _size, match);
        }

        /// <summary>
        /// Starting with index specified by <paramref name="startIndex"/>, find the index of the first item that matches the specified predicate.
        /// </summary>
        /// <param name="startIndex">the starting index</param>
        /// <param name="match">the predicate</param>
        /// <returns>The first index at or after <paramref name="startIndex"/> where the item satisfied <paramref name="match"/>; a negative number if no such element.</returns>
        ///  <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
        public int FindIndex(int startIndex, RefPredicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < startIndex + Count);
            return FindIndex(startIndex, _size - startIndex, match);
        }

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
        public int FindIndex(int startIndex, int count, RefPredicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if ((uint)startIndex > (uint)_size) throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex,
                @"Parameter exceeds the size of the list: {_size}.");
            if (count < 0 || startIndex > _size - count)
            {
                throw new ArgumentException(
                    $"Either {nameof(count)} (value: {count}) is " +
                    $"negative or {nameof(startIndex)} (value: {startIndex}) " +
                    $"is greater than the size of the collection " +
                    $"(value: {_size}) less {nameof(count)} " +
                    $"(value: {count}) (value of difference: {_size - count}).");
            }
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < startIndex + count);
            Contract.EndContractBlock();

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                ref readonly T val = ref _items[i];
                if (match(in val)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Find the index of the last item in the collection that satisfies the predicate supplied
        /// by <paramref name="match"/>.
        /// </summary>
        /// <param name="match">The predicate to supply.</param>
        /// <returns>The index of the last item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        public int FindLastIndex(RefPredicate<T> match)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            return FindLastIndex(_size - 1, _size, match);
        }

        /// <summary>
        /// Find the index of the last item (starting with <paramref name="startIndex"/> and working backwards) that satisfies the predicate.
        /// </summary>
        /// <param name="startIndex">The index to begin from (working backwards).</param>
        /// <param name="match">The predicate</param>
        /// <returns>The index of the last item that satisfied <paramref name="match"/> or a negative number if no such.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
        public int FindLastIndex(int startIndex, RefPredicate<T> match)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() <= startIndex);
            return FindLastIndex(startIndex, startIndex + 1, match);
        }

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
        public int FindLastIndex(int startIndex, int count, RefPredicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() <= startIndex);
            Contract.EndContractBlock();

            if (_size == 0)
            {
                // Special case for 0 length List
                if (startIndex != -1)
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex,
                        @"List is empty and start index does not equal -1.");
                }
            }
            else
            {
                // Make sure we're not out of range            
                if ((uint)startIndex >= (uint)_size)
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex,
                        $@"Parameter is greater than or equal to the size of the list.  (Count: {_size}).");
                }
            }

            // 2nd have of this also catches when startIndex == MAXINT, so MAXINT - 0 + 1 == -1, which is < 0.
            if (count < 0 || startIndex - count + 1 < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), @"Count is negative or out of range.");
            }

            int endIndex = startIndex - count;
            for (int i = startIndex; i > endIndex; i--)
            {
                ref readonly T val = ref _items[i];
                if (match(in val))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Perform a (potentially mutating) action on every item in the collection.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> was null.</exception>
        public void ForEach(MutatingRefAction<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Contract.EndContractBlock();

            for (int i = 0; i < _size; i++)
            {
                ref var val = ref _items[i];
                action(ref val);
            }
        }

        /// <summary>
        /// Perform a non-mutating action on each item in the collection (e.g. write to output or something)
        /// </summary>
        /// <param name="action">the action</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> was null.</exception>
        public void ForEachI(RefAction<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Contract.EndContractBlock();

            for (int i = 0; i < _size; i++)
            {
                ref readonly var val = ref _items[i];
                action(in val);
            }
        }

        /// <summary> Returns the index of the first occurrence of a given value in a range of
        ///this list. The list is searched forwards from beginning to end.
        ///The elements of the list are compared to the given value using the
        ///Object.Equals method.
        /// </summary>
        /// <returns>The index of the item or a negative number if not found.</returns>
        /// <remarks> This method uses the Array.IndexOf method to perform the
        /// search.</remarks>
        public int IndexOf(in T item)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            return Array.IndexOf(_items, item, 0, _size);
        }



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
        public int IndexOf(in T item, int index)
        {
            if (index >= _size)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    @$"Parameter must be less than the size of the list (value: {Count})");
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();
            return Array.IndexOf(_items, item, index, _size - index);
        }

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
        public int IndexOf(in T item, int index, int count)
        {
            if (index > _size)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    @$"{nameof(index)} (value: {index}) may not be greater than the size (value: {Count}) of the collection");
            if (count < 0)
                throw new ArgumentNegativeException<int>(nameof(count), count);
            if (index > _size - count)
                throw new ArgumentException(
                    "Parameter is greater than the size of the collection (value: {_size}) - {nameof(count)} (value: {count}).");
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();

            return Array.IndexOf(_items, item, index, count);
        }

        /// <summary> Inserts an element into this list at a given index. The size of the list
        /// is increased by one. If required, the capacity of the list is doubled
        /// before inserting the new element.</summary>
        /// <param name="index">The index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The item to insert at <paramref name="index"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or not less than or equal to the size of the collection.</exception>
        public void Insert(int index, in T item)
        {
            // Note that insertions at the end are legal.
            if ((uint)index > (uint)_size) throw new ArgumentOutOfRangeException(nameof(index), index,
                @"Parameter must be non-negative and less than or equal to the size of the collection.");

            Contract.EndContractBlock();
            if (_size == _items.Length) EnsureCapacity(_size + 1);
            if (index < _size)
            {
                Array.Copy(_items, index, _items, index + 1, _size - index);
            }
            _items[index] = item;
            _size++;
        }

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to insert <paramref name="collection"/>.</param>
        /// <param name="collection"> the collection to insert at <paramref name="index"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        public void InsertRange(int index, VsEnumerableWrapper<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));


            // Note that insertions at the end are legal.
            if ((uint)index > (uint)_size) throw new ArgumentOutOfRangeException(nameof(index), index,
                @"Parameter must be non-negative and less than or equal to the size of the collection.");
            Contract.EndContractBlock();
            {
                using (var en = collection.GetEnumerator())
                {
                    while (en.MoveNext())
                    {
                        Insert(index++, en.Current);
                    }
                }
            }
        }

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to start inserting the items in <paramref name="c"/>.</param>
        /// <param name="c">the collection whose items you wish to insert.</param>
        /// <exception cref="ArgumentNullException"><paramref name="c"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        public void InsertRange(int index, VsListWrapper<T> c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            // Note that insertions at the end are legal.
            if ((uint)index > (uint)_size) throw new ArgumentOutOfRangeException(nameof(index), index,
                @"Parameter must be non-negative and less than or equal to the size of the collection.");
            int count = c.Count;
            if (count > 0)
            {
                EnsureCapacity(_size + count);
                if (index < _size)
                {
                    Array.Copy(_items, index, _items, index + count, _size - index);
                }
                T[] itemsToInsert = new T[count];
                c.CopyTo(itemsToInsert, 0);
                itemsToInsert.CopyTo(_items, index);

                _size += count;
            }
        }

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to start inserting the items in <paramref name="c"/>.</param>
        /// <param name="c">the collection whose items you wish to insert.</param>
        /// <exception cref="ArgumentNullException"><paramref name="c"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        public void InsertRange(int index, VsArrayWrapper<T> c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            // Note that insertions at the end are legal.
            if ((uint)index > (uint)_size) throw new ArgumentOutOfRangeException(nameof(index), index,
                @"Parameter must be non-negative and less than or equal to the size of the collection.");
            int count = c.Count;
            if (count > 0)
            {
                EnsureCapacity(_size + count);
                if (index < _size)
                {
                    Array.Copy(_items, index, _items, index + count, _size - index);
                }
                T[] itemsToInsert = new T[count];
                c.CopyTo(itemsToInsert, 0);
                itemsToInsert.CopyTo(_items, index);

                _size += count;
            }
        }

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to start inserting the items in <paramref name="c"/>.</param>
        /// <param name="c">the collection whose items you wish to insert.</param>
        /// <exception cref="ArgumentNullException"><paramref name="c"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        public void InsertRange(int index, ByRefList<T> c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if ((uint)index > (uint)_size) throw new ArgumentOutOfRangeException(nameof(index), index,
                @"Parameter must be non-negative and less than or equal to the size of the collection.");
            Contract.EndContractBlock();
            int count = c.Count;

            if (count > 0)
            {
                EnsureCapacity(_size + count);
                if (index < _size)
                {
                    Array.Copy(_items, index, _items, index + count, _size - index);
                }

                if (ReferenceEquals(this, c))
                {
                    // Copy first part of _items to insert location
                    Array.Copy(_items, 0, _items, index, index);
                    // Copy last part of _items back to inserted location
                    Array.Copy(_items, index + count, _items, index * 2, _size - index);

                }
                else
                {

                    T[] itemsToInsert = new T[count];
                    c.CopyTo(itemsToInsert, 0);
                    itemsToInsert.CopyTo(_items, index);
                }
                _size += count;
            }
        }



        /// <summary> Returns the index of the last occurrence of a given value in a range of
        /// this list. The list is searched backwards, starting at the end 
        /// and ending at the first element in the list. The elements of the list 
        /// are compared to the given value using the Object.Equals method. </summary>
        /// <remarks> This method uses the Array.LastIndexOf method to perform the
        /// search. </remarks>
        /// <returns>The index of the last occurence of item if found, a negative number otherwise.</returns>
        public int LastIndexOf(in T item)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            return _size == 0 ? -1 : LastIndexOf(in item, _size - 1, _size);
        }

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
        public int LastIndexOf(in T item, int index)
        {
            if (index >= _size)
                throw new ArgumentOutOfRangeException(nameof(index), index,
            @"Parameter must be less than the size of the collection.");
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(((Count == 0) && (Contract.Result<int>() == -1)) || ((Count > 0) && (Contract.Result<int>() <= index)));
            Contract.EndContractBlock();
            return LastIndexOf(in item, index, index + 1);
        }

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
        public int LastIndexOf(in T item, int index, int count)
        {
            if ((Count != 0) && (index < 0))
            {
                throw new ArgumentNegativeException<int>(nameof(index), index, "Parameter may not be negative unless the collection is empty.");
            }

            if ((Count != 0) && (count < 0))
            {
                throw new ArgumentNegativeException<int>(nameof(count), count, "Parameter may not be negative unless the collection is empty.");
            }
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(((Count == 0) && (Contract.Result<int>() == -1)) || ((Count > 0) && (Contract.Result<int>() <= index)));
            Contract.EndContractBlock();

            if (_size == 0)
            {  // Special case for empty list
                return -1;
            }

            if (index >= _size)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, @"Parameter must be less than size of collection.");
            }

            if (count > index + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count,
                    @$"Parameter may not exceed parameter {nameof(index)} (value: {index}) by more than one.");
            }

            return Array.LastIndexOf(_items, item, index, count);
        }

        /// <summary>Removes the element if found. If found, the size of the list is
        /// decreased by one.</summary>
        /// <returns>True if the item was found and removed, false otherwise</returns>
        public bool Remove(in T item)
        {
            int index = IndexOf(in item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }



        /// <summary> This method removes all items which matches the predicate. </summary>
        /// <param name="match">the predicate</param>
        /// <returns>the number of items removed.</returns>
        /// <remarks>The complexity is O(n).   </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        public int RemoveAll(RefPredicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));

            Contract.Ensures(Contract.Result<int>() >= 0);
            Contract.Ensures(Contract.Result<int>() <= Contract.OldValue(Count));
            Contract.EndContractBlock();

            int freeIndex = 0;   // the first free slot in items array

            // Find the first item which needs to be removed.
            while (freeIndex < _size && !match(in _items[freeIndex])) freeIndex++;
            if (freeIndex >= _size) return 0;

            int current = freeIndex + 1;
            while (current < _size)
            {
                // Find the first item which needs to be kept.
                while (current < _size && match(in _items[current])) current++;

                if (current < _size)
                {
                    // copy item to the free slot.
                    _items[freeIndex++] = _items[current++];
                }
            }

            Array.Clear(_items, freeIndex, _size - freeIndex);
            int result = _size - freeIndex;
            _size = freeIndex;
            return result;
        }

        /// <summary>Removes the element at the given index. The size of the list is
        /// decreased by one.</summary>
        /// <param name="index">The index of the item that should be removed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> outside the bounds of the list.
        /// </exception>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_size)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    @"Parameter must be non-negative and less than the size of the collection.");
            }
            Contract.EndContractBlock();
            _size--;
            if (index < _size)
            {
                Array.Copy(_items, index + 1, _items, index, _size - index);
            }
            _items[_size] = default(T);
        }

        /// <summary> Removes a range of elements from this list.</summary>
        /// <param name="index">Index of the first item to remove.</param>
        /// <param name="count">number of items to remove</param>
        /// <exception cref="ArgumentNegativeException{T}"><paramref name="index"/> or
        /// <paramref name="count"/> was negative.</exception>
        /// <exception cref="ArgumentException">Taken together, <paramref name="index"/> and <paramref name="count"/> do not denote a valid range.</exception>
        public void RemoveRange(int index, int count)
        {
            if (index < 0) throw new ArgumentNegativeException<int>(nameof(index), index);
            if (count < 0) throw new ArgumentNegativeException<int>(nameof(count), count);

            if (_size - index < count)
                throw new ArgumentException(
                    "The size of the collection " +
                    $"(value: {Count}) less parameter {nameof(index)} " +
                    $"(value: {index}; value of difference: {_size - index}) " +
                    $"must be less than parameter {nameof(count)} (value: {count}).");
            Contract.EndContractBlock();

            if (count > 0)
            {
                _size -= count;
                if (index < _size)
                {
                    Array.Copy(_items, index + count, _items, index, _size - index);
                }
                Array.Clear(_items, _size, count);
            }
        }

        /// <summary> Reverses the elements in this list. </summary>
        /// <remarks>Uses the Array.Reverse method</remarks>
        public void Reverse()
        {
            Reverse(0, Count);
        }

        /// <summary> Reverses the elements in a range of this list. Following a call to this
        /// method, an element in the range given by  <paramref name="index"/>and <paramref name="count"/>
        /// which was previously located at index i will now be located at
        /// index: index + (index + count - i - 1).</summary>
        /// <param name="index">starting index of the range to reverse</param>
        /// <param name="count">number of items in range to be reversed</param>
        /// <remarks>This method uses the Array.Reverse method to reverse the
        /// elements.</remarks>
        /// <exception cref="ArgumentNegativeException{T}"><paramref name="index"/> or
        /// <paramref name="count"/> was negative.</exception>
        /// <exception cref="ArgumentException">Taken together, <paramref name="index"/> and <paramref name="count"/> do not denote a valid range.</exception>
        public void Reverse(int index, int count)
        {
            if (index < 0) throw new ArgumentNegativeException<int>(nameof(index), index);

            if (count < 0) throw new ArgumentNegativeException<int>(nameof(count), count);


            if (_size - index < count)
                throw new ArgumentException(
                    $"The size of the collection (value: {Count}) - parameter {nameof(index)} (value: {index}; value of difference: {_size - index}) must be less than parameter {nameof(count)} (value: {count}).");
            Contract.EndContractBlock();
            Array.Reverse(_items, index, count);
        }


        /// <summary>Sorts the elements in this list.  Uses the default comparer and 
        /// Array.Sort.</summary> 
        public void Sort()
        {
            Sort(0, Count, null);
        }

        /// <summary> Sorts the elements in this list.</summary>
        /// <param name="comparer">comparer to use, default will be used if null.</param>
        /// <remarks>Uses Array.Sort with the provided comparer.</remarks>
        public void Sort(IComparer<T> comparer)
        {
            Sort(0, Count, comparer);
        }

        /// <summary>Sorts the elements in a section of this list. The sort compares the
        /// elements to each other using the given IComparer interface. If
        /// comparer is null, the elements are default compared. </summary>
        ///<param name="index">The starting index of the range to be sorted.</param>
        /// <param name="count">The number of items in the range to be sorted.</param>
        /// <param name="comparer">The comparer</param>
        /// <remarks> This method uses the Array.Sort method to sort the elements.</remarks>
        /// <exception cref="ArgumentNegativeException{T}"><paramref name="index"/> or
        /// <paramref name="count"/> was negative.</exception>
        /// <exception cref="ArgumentException">Taken together, <paramref name="index"/> and <paramref name="count"/>
        /// do not denote a valid range.</exception>
        public void Sort(int index, int count, IComparer<T> comparer)
        {
            if (index < 0) throw new ArgumentNegativeException<int>(nameof(index), index);

            if (count < 0) throw new ArgumentNegativeException<int>(nameof(count), count);


            if (_size - index < count)
                throw new ArgumentException(
                    $"The size of the collection (value: {Count}) - parameter {nameof(index)} (value: {index}; value of difference: {_size - index}) must be less than parameter {nameof(count)} (value: {count}).");
            Contract.EndContractBlock();

            Array.Sort(_items, index, count, comparer);

        }



        /// <summary> ToArray returns a new immutable array containing the contents of the List. </summary>
        /// <remarks> This requires copying the List, which is an O(n) operation.</remarks>
        public ImmutableArray<T> ToArray()
        {
            Contract.Ensures(!Contract.Result<ImmutableArray<T>>().IsDefault);
            Contract.Ensures(Contract.Result<ImmutableArray<T>>().Length == Count);
            return ImmutableArray.Create(_items, 0, Count);
        }

        /// <summary> Sets the capacity of this list to the size of the list. This method can
        /// be used to minimize a list's memory overhead once it is known that no
        /// new elements will be added to the list. </summary>
        ///
        /// <remarks>To completely clear a list and
        /// release all memory referenced by the list, execute the following
        /// statements:
        /// 
        /// list.Clear();
        /// list.TrimExcess();
        /// </remarks>
        public void TrimExcess()
        {
            int threshold = (int)(_items.Length * 0.9);
            if (_size < threshold)
            {
                Capacity = _size;
            }
        } 
        #endregion
        
        #region Private Methods
        private protected abstract string GetStringRepresentation();

        // Ensures that the capacity of this list is at least the given minimum
        // value. If the current capacity of the list is less than min, the
        // capacity is increased to twice the current capacity or to min,
        // whichever is larger.
        private void EnsureCapacity(int min)
        {
            if (_items.Length < min)
            {
                int newCapacity;
                checked
                {
                    newCapacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;
                }
                Capacity = min > newCapacity ? min : newCapacity;
            }
        }

        private protected void CopyTo(T[] itemsToInsert, int idx) => _items.CopyTo(itemsToInsert, idx); 
        #endregion

        #region Privates
        private protected const int DefaultCapacity = 4;
        [NotNull] private protected T[] _items;
        private protected int _size;
        private protected static readonly T TheDefaultValue = default;
        private protected static T[] TheEmptyArray => Array.Empty<T>(); 
        #endregion
    }


    partial class ByRefList<T> //contains nested typedefs
    {
        /// <summary>
        /// Can be used in for each to enumerate index item pairs as filtered.
        /// </summary>
        public readonly ref struct IndexAndItemFilteredCollectionView 
        {

            /// <summary>
            /// Get the enumerator
            /// </summary>
            /// <returns>An enumerator</returns>
            /// <exception cref="InvalidOperationException">View is not properly initialize</exception>
            public IndexItemFilteringEnumerator GetEnumerator()
            {
                try
                {
                    return IndexItemFilteringEnumerator.CreateIndexItemFilteringEnumerator(_wrapped, _filter);
                }
                catch (NullReferenceException ex)
                {
                    throw new InvalidOperationException("The collection view has not properly been initialized.", ex);
                }
            }

            internal IndexAndItemFilteredCollectionView([NotNull] ByRefList<T> wrapped, [NotNull] RefPredicate<T> predicate)
            {
                _wrapped = wrapped ?? throw new ArgumentNullException(nameof(wrapped));
                _filter = predicate ?? throw new ArgumentNullException(nameof(predicate));
            }

            [NotNull] private readonly ByRefList<T> _wrapped;
            [NotNull] private readonly RefPredicate<T> _filter;
        }
        /// <summary>
        /// An enumerator that -- given a list and a predicate permits lazy enumeration of all
        /// the indices / items that match the given predicate.  In addition to normal enumerable methods, it permits 
        /// enumeration of all index/value pairs to an immutable array.
        /// </summary>
        public ref struct IndexItemFilteringEnumerator
        {
            /// <summary>
            /// Create an enumerator
            /// </summary>
            /// <param name="owningList">the owning list</param>
            /// <param name="predicate">the predicate used to determine which items/indices you
            /// wish to enumerate</param>
            /// <returns>An enumerator</returns>
            internal static IndexItemFilteringEnumerator CreateIndexItemFilteringEnumerator([NotNull] ByRefList<T> owningList, [NotNull] RefPredicate<T> predicate) =>
                new IndexItemFilteringEnumerator(predicate, (owningList ?? throw new ArgumentNullException(nameof(owningList)))._items, owningList.Count);

            /// <summary>
            /// Gets the currently enumerated item
            /// </summary>
            /// <exception cref="InvalidOperationException">There is no valid currently enumerated item.</exception>
            public readonly IndexItemPair Current
            {
                get
                {
                    try
                    {
                        return new IndexItemPair(_itemsArray, _currentIndex);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new InvalidOperationException("The enumerator is not currently in a valid state.", ex);
                    }
                }
            }

            /// <summary>
            /// Move to the next item if possible
            /// </summary>
            /// <returns>True if it successfully found another item.</returns>
            public bool MoveNext()
            {
                try
                {
                    bool isMatch;
                    do
                    {
                        if (++_currentIndex < _itemsCount)
                        {
                            ref readonly var item = ref _itemsArray[_currentIndex];
                            isMatch = _predicate(in item);
                        }
                        else
                        {
                            isMatch = false;
                        }
                    } while (_currentIndex < _itemsArray.Length && !isMatch);

                    return isMatch;
                }
                catch (NullReferenceException ex)
                {
                    throw new InvalidOperationException(
                        "The enumerator has not been properly initialized -- its internal array is null.", ex);
                }
            }

            /// <summary>
            /// Reset the enumerator.
            /// </summary>
            public void Reset() => _currentIndex = -1;

            /// <summary>
            /// Without changing the state of this enumerator, enumerate all indices / items to an immutable array.
            /// </summary>
            /// <returns>An immutable array containing copies the indices and items</returns>
            public readonly ImmutableArray<(int Index, T Item)> EnumerateAllToArray()
            {
                try
                {
                    var bldr = ImmutableArray.CreateBuilder<(int Index, T Item)>();
                    var copyOfMe = new IndexItemFilteringEnumerator(_predicate, _itemsArray, _itemsCount);
                    while (copyOfMe.MoveNext())
                    {
                        var current = copyOfMe.Current;
                        bldr.Add((current.Index, current.Item));
                    }

                    return bldr.Capacity == bldr.Count ? bldr.MoveToImmutable() : bldr.ToImmutable();
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException("This enumerator has not been properly initialized.", ex);
                }
            }

            private IndexItemFilteringEnumerator([NotNull] RefPredicate<T> predicate, T[] itemArray, int listCount)
            {
                _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
                _itemsArray = itemArray ?? throw new ArgumentNullException(nameof(itemArray));
                _currentIndex = -1;
                _itemsCount = listCount;
                Debug.Assert(_itemsCount <= _itemsArray.Length);
            }


            private readonly int _itemsCount;
            [NotNull] private readonly RefPredicate<T> _predicate;
            private int _currentIndex;
            [NotNull] private readonly T[] _itemsArray;
        }

        /// <summary>
        /// An index item pair (optimized for large value types)
        /// Size here is size of reference to array + index.
        /// </summary>
        [VaultSafe(true)]
        [NotVsProtectable]
        public ref struct IndexItemPair
        {
            /// <summary>
            /// True if this is a properly initialized index item pair, false otherwise
            /// </summary>
            public bool IsValid => _items != null && _items.Length > 0 && Index > -1 && Index < _items.Length;
            /// <summary>
            /// The index of the item
            /// </summary>
            public readonly int Index;
            /// <summary>
            /// A readonly reference to the item itself.
            /// </summary>
            public ref readonly T Item => ref _items[Index];

            internal IndexItemPair([NotNull] T[] itemArr, int index)
            {
                if (itemArr == null) throw new ArgumentNullException(nameof(itemArr));
                if (index < 0 || index >= itemArr.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), index,
                        @"Parameter outside bounds of the array.");
                _items = itemArr;
                Index = index;
            }

            private readonly T[] _items;
        }

        /// <summary>
        /// An efficient struct enumerator.  Version safety checks disabled ... making changes to
        /// collection while enumerator live can result in unpredictable wrong behavior.  By being a ref struct that
        /// can only live on the stack, the potential mischief is mitigated.  Also, since designed to be used together with
        /// vaults, thread safety concerns also mitigated.
        /// </summary>
        [VaultSafe(true)]
        [NotVsProtectable]
        public ref struct Enumerator
        {
            /// <summary>
            /// Retrieve the current value
            /// </summary>
            /// <exception cref="IndexOutOfRangeException">The enumerator is not in a state where there is currently a valid value.</exception>
            public ref readonly T Current => ref _list[_index];

            internal Enumerator([NotNull] ByRefList<T> list)
            {
                _list = (list ?? throw new ArgumentNullException(nameof(list)))._items;
                _index = -1;
                _count = list.Count;
                Debug.Assert(_count <= _list.Length );
            }

            /// <summary>
            /// Advance to the next element
            /// </summary>
            /// <returns>true if the enumerator refers to a valid element after this call,
            /// false otherwise.</returns>
            public bool MoveNext()
            {
                ++_index;
                return _index > -1 && _index < _count;
            }

            /// <summary>
            /// Reset the enumerator
            /// </summary>
            public void Reset() => _index = -1;

            private readonly T[] _list;
            private readonly int _count;
            private int _index;

        }

        /// <summary>
        /// An efficient struct enumerator.  Version safety checks disabled ... making changes to
        /// collection while enumerator live can result in unpredictable wrong behavior.  By being a ref struct that
        /// can only live on the stack, the potential mischief is mitigated.  Also, since designed to be used together with
        /// vaults, thread safety concerns also mitigated.
        /// </summary>
        [VaultSafe(true)]
        [NotVsProtectable]
        public ref struct WhereEnumerator
        {
            /// <summary>
            /// Retrieve the current value
            /// </summary>
            /// <remarks>If MoveNext has not been called with a true result, accessing this property
            /// results in undefined behavior.</remarks>
            /// <exception cref="IndexOutOfRangeException">The enumerator is not in a state where there is currently a valid value.</exception>
            public ref readonly T Current => ref _list[_index];

            internal WhereEnumerator(ByRefList<T> list, [NotNull] RefPredicate<T> filter)
            {
                _list = (list ?? throw new ArgumentNullException(nameof(list)))._items;
                _index = -1;
                _filter = filter ?? throw new ArgumentNullException(nameof(filter));
                _count = list.Count;
                Debug.Assert(_count <= _list.Length);
            }

            /// <summary>
            /// Advance to the next element
            /// </summary>
            /// <returns>true if the enumerator refers to a valid element after this call,
            /// false otherwise.</returns>
            public bool MoveNext()
            {
                while (true)
                {
                    ++_index;
                    if (_index < 0 || _index >= _count)
                    {
                        return false;
                    }
                    if (_filter(in _list[_index]))
                    {
                        return true;
                    }
                }
                
            }

            /// <summary>
            /// Reset the enumerator
            /// </summary>
            public void Reset() => _index = -1;

            private readonly int _count;
            private readonly T[] _list;
            private int _index;
            private readonly RefPredicate<T> _filter;
        }

        /// <summary>
        /// An efficient struct enumerator.  Version safety checks disabled ... making changes to
        /// collection while enumerator live can result in unpredictable wrong behavior.  By being a ref struct that
        /// can only live on the stack, the potential mischief is mitigated.  Also, since designed to be used together with
        /// vaults, thread safety concerns also mitigated.
        /// </summary>
        [VaultSafe(true)]
        [NotVsProtectable]
        public ref struct SelectEnumerator<[VaultSafeTypeParam] TTransformTo>
        {
            /// <summary>
            /// Retrieve the current value
            /// </summary>
            /// <exception cref="IndexOutOfRangeException">The enumerator is not in a state where there is currently a valid value.</exception>
            public readonly TTransformTo Current => _transformer(in _list[_index]);

            internal SelectEnumerator(ByRefList<T> list, [NotNull] RefFunc<T, TTransformTo> transformer)
            {
                _list = (list ?? throw new ArgumentNullException(nameof(list)))._items;
                _index = -1;
                _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
                _count = list.Count;
                Debug.Assert(_count <= _list.Length);
            }

            /// <summary>
            /// Advance to the next element
            /// </summary>
            /// <returns>true if the enumerator refers to a valid element after this call,
            /// false otherwise.</returns>
            public bool MoveNext()
            {
                ++_index;
                return _index > -1 && _index < _count;
            }

            /// <summary>
            /// 
            /// </summary>
            public void Reset() => _index = -1;

            private readonly int _count;
            private readonly T[] _list;
            private int _index;
            private readonly RefFunc<T, TTransformTo> _transformer;
        }

        /// <summary>
        /// An efficient struct enumerator.  Version safety checks disabled ... making changes to
        /// collection while enumerator live can result in unpredictable wrong behavior.  By being a ref struct that
        /// can only live on the stack, the potential mischief is mitigated.  Also, since designed to be used together with
        /// vaults, thread safety concerns also mitigated.
        /// </summary>
        [VaultSafe(true)]
        [NotVsProtectable]
        public ref struct SelectWhereEnumerator<[VaultSafeTypeParam] TTransformTo>
        {
            /// <summary>
            /// Retrieve the current value
            /// </summary>
            /// <exception cref="IndexOutOfRangeException">The enumerator is not in a state where there is currently a valid value.</exception>
            public readonly TTransformTo Current => _transformer(in _list[_index]);

            internal SelectWhereEnumerator(ByRefList<T> list, [NotNull] RefPredicate<T> filter, [NotNull] RefFunc<T, TTransformTo> transformer)
            {
                _list = (list ?? throw new ArgumentNullException(nameof(list)))._items;
                _filter = filter;
                _index = -1;
                _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
                _count = list.Count;
                Debug.Assert(_count <= _list.Length);
            }

            /// <summary>
            /// Advance to the next element
            /// </summary>
            /// <returns>true if the enumerator refers to a valid element after this call,
            /// false otherwise.</returns>
            public bool MoveNext()
            {
                while (true)
                {
                    ++_index;
                    if (_index < 0 || _index >= _count)
                    {
                        return false;
                    }
                    if (_filter(in _list[_index]))
                    {
                        return true;
                    }
                }
            }

            /// <summary>
            /// Reset the enumerator
            /// </summary>
            public void Reset() => _index = -1;

            private readonly T[] _list;
            private int _index;
            private readonly RefFunc<T, TTransformTo> _transformer;
            private readonly RefPredicate<T> _filter;
            private readonly int _count;
        }
    }
}
