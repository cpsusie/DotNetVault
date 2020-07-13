using System;
using System.Collections.Generic;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.RefReturningCollections
{
    /// <summary>
    /// A list designed to be similar to the built in list but
    /// which returns by reference and will work with a special readwrite vault
    /// </summary>
    /// <typeparam name="T">The type parameter.  Must be vault-safe.</typeparam>
    public interface IByRefList<[VaultSafeTypeParam] T> : IReadOnlyByRefList<T>
    {
        /// <summary>
        /// Gets and sets the capacity of this list.The capacity is the size of 
        ///the internal array used to hold items.When set, the internal
        /// array of the list is reallocated to the given capacity.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/>
        /// was less than the current size of the list.</exception>
        new int Capacity { get; set; }
        /// <summary>
        /// Get the item at the specified index by mutable reference
        /// </summary>
        /// <param name="index">the index</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was out of range.</exception>
        new ref T this[int index] { get; }

        ///<summary>
        /// Adds the given object to the end of this list. The size of the list is
        /// increased by one. If required, the capacity of the list is doubled
        /// before adding the new element.
        ///</summary> 
        void Add(in T item);

        /// <summary>Adds the elements of the given collection to the end of this list. If
        ///required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        void AddRange([NotNull] VsEnumerableWrapper<T> collection);

        /// <summary>Adds the elements of the given collection to the end of this list. If
        ///required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        void AddRange([NotNull] VsListWrapper<T> collection);

        /// <summary>Adds the elements of the given collection to the end of this list. If
        ///required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        void AddRange([NotNull] VsArrayWrapper<T> collection);

        ///<summary>Clears the contents of List. </summary>
        void Clear();

        /// <summary>
        /// Search the list to see if any item matches the predicate and if so, return the
        /// first item that does.
        /// </summary>
        /// <param name="match">the predicate</param>
        /// <returns>A tuple whose FoundAny member indicates whether such an item was found.  If FoundAny
        /// is true, Value represents the found value, otherwise value will be default.</returns>
        (bool FoundAny, T Value) Find([NotNull] RefPredicate<T> match);

        /// <summary>
        /// Perform a (potentially mutating) action on every item in the collection.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> was null.</exception>
        void ForEach([NotNull] MutatingRefAction<T> action);

        /// <summary>
        /// Perform a non-mutating action on each item in the collection (e.g. write to output or something)
        /// </summary>
        /// <param name="action">the action</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> was null.</exception>
        void ForEachI([NotNull] RefAction<T> action);

        /// <summary> Inserts an element into this list at a given index. The size of the list
        /// is increased by one. If required, the capacity of the list is doubled
        /// before inserting the new element.</summary>
        /// <param name="index">The index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The item to insert at <paramref name="index"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or not less than or equal to the size of the collection.</exception>
        void Insert(int index, in T item);

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to insert <paramref name="collection"/>.</param>
        /// <param name="collection"> the collection to insert at <paramref name="index"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        void InsertRange(int index, [NotNull] VsEnumerableWrapper<T> collection);

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to start inserting the items in <paramref name="c"/>.</param>
        /// <param name="c">the collection whose items you wish to insert.</param>
        /// <exception cref="ArgumentNullException"><paramref name="c"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        void InsertRange(int index, [NotNull] VsListWrapper<T> c);

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to start inserting the items in <paramref name="c"/>.</param>
        /// <param name="c">the collection whose items you wish to insert.</param>
        /// <exception cref="ArgumentNullException"><paramref name="c"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        void InsertRange(int index, [NotNull] VsArrayWrapper<T> c);

        /// <summary> Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size, whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size. </summary>
        /// <param name="index">the index at which to start inserting the items in <paramref name="c"/>.</param>
        /// <param name="c">the collection whose items you wish to insert.</param>
        /// <exception cref="ArgumentNullException"><paramref name="c"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> was negative or greater than the size of the collection.</exception>
        void InsertRange(int index, ByRefList<T> c);

        /// <summary>Removes the element if found. If found, the size of the list is
        /// decreased by one.</summary>
        /// <returns>True if the item was found and removed, false otherwise</returns>
        bool Remove(in T item);

        /// <summary> This method removes all items which matches the predicate. </summary>
        /// <param name="match">the predicate</param>
        /// <returns>the number of items removed.</returns>
        /// <remarks>The complexity is O(n).   </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        int RemoveAll([NotNull] RefPredicate<T> match);

        /// <summary>Removes the element at the given index. The size of the list is
        /// decreased by one.</summary>
        /// <param name="index">The index of the item that should be removed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> outside the bounds of the list.
        /// </exception>
        void RemoveAt(int index);

        /// <summary> Removes a range of elements from this list.</summary>
        /// <param name="index">Index of the first item to remove.</param>
        /// <param name="count">number of items to remove</param>
        /// <exception cref="ArgumentNegativeException{T}"><paramref name="index"/> or
        /// <paramref name="count"/> was negative.</exception>
        /// <exception cref="ArgumentException">Taken together, <paramref name="index"/> and <paramref name="count"/> do not denote a valid range.</exception>
        void RemoveRange(int index, int count);

        /// <summary> Reverses the elements in this list. </summary>
        /// <remarks>Uses the Array.Reverse method</remarks>
        void Reverse();

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
        void Reverse(int index, int count);

        /// <summary>Sorts the elements in this list.  Uses the default comparer and 
        /// Array.Sort.</summary> 
        void Sort();

        /// <summary> Sorts the elements in this list.</summary>
        /// <param name="comparer">comparer to use, default will be used if null.</param>
        /// <remarks>Uses Array.Sort with the provided comparer.</remarks>
        void Sort([CanBeNull] IComparer<T> comparer);

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
        void Sort(int index, int count, [CanBeNull] IComparer<T> comparer);

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
        void TrimExcess();
    }
}