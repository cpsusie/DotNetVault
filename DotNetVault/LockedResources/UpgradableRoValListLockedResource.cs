﻿using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using DotNetVault.RefReturningCollections;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.LockedResources
{
    /// <summary>
    /// This readonly locked resource type is returned by <see cref=" ReadWriteValueListVault{TItem}"/>s whose generic parameter is vault-safe
    /// when a non-upgradable read-only lock is requested.  It exposes methods and properties that cannot in any way change make changes to the list
    /// itself or to the values of its contents.
    /// </summary>
    /// <typeparam name="TVault">The vault type</typeparam>
    /// <typeparam name="TItem">The vault-safe self-equatable and self-comparable value type held by the list.</typeparam>
    [NoCopy]
    [RefStruct]
    public readonly ref struct UpgradableRoValListLockedResource<TVault, [VaultSafeTypeParam] TItem>
        where TVault : ReadWriteListVault<TItem, BigValueList<TItem>> where TItem : struct, IEquatable<TItem>, IComparable<TItem>
    {
        #region Factories
        internal static UpgradableRoValListLockedResource<TVault, TItem> CreateUpgradableReadOnlyLockedResource([NotNull] TVault v,
            [NotNull] Vault<BigValueList<TItem>>.Box b, [NotNull] Action<TimeSpan?, CancellationToken> upgradeAction, [NotNull] Action upgradeForeverAction)
        {
            Func<ReadWriteListVault<TItem, BigValueList<TItem>>, Vault<BigValueList<TItem>>.Box, AcquisitionMode, Vault<BigValueList<TItem>>.Box> releaseMethod =
                ReadWriteVault<BigValueList<TItem>>.ReleaseResourceMethod;
            return new UpgradableRoValListLockedResource<TVault, TItem>(v, b, releaseMethod, upgradeAction, upgradeForeverAction);
        }
        #endregion

        #region Public Properties and Indexer
        #region Resource / Vault Related
        /// <summary>
        /// True if this resource object is initialized and not disposed, false otherwise
        /// </summary>
        public bool IsGood => _wrapped.IsInitialized && _flag?.IsSet == false;

        /// <summary>
        /// The timeout used when obtaining a lock but not specifying a timeout
        /// </summary>
        public TimeSpan DefaultTimeout { get; }

        #endregion

        #region ReadOnlyList-Related

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
        public ref readonly TItem this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _wrapped[index];
        }
        #endregion
        #endregion

        #region Private CTOR
        private UpgradableRoValListLockedResource([NotNull] TVault v, [NotNull] Vault<BigValueList<TItem>>.Box b,
            [NotNull]Func<ReadWriteListVault<TItem, BigValueList<TItem>>, Vault<BigValueList<TItem>>.Box, AcquisitionMode, Vault<BigValueList<TItem>>.Box> disposeMethod, [NotNull] Action<TimeSpan?, CancellationToken> upgradeAction, [NotNull] Action upgradeForeverAction)
        {
            Debug.Assert(b != null && v != null && disposeMethod != null);
            _box = b;
            _disposeMethod = disposeMethod;
            _flag = new DisposeFlag();
            _vault = v;
            DefaultTimeout = _vault.DefaultTimeout;
            _wrapped = b.Value.GetReadOnlyView();
            _upgradeAction = upgradeAction;
            _upgradeForeverAction = upgradeForeverAction;
            Debug.Assert(_wrapped.IsInitialized);
        }
        #endregion

        #region Dispose Method and Public Upgrade Methods
        /// <summary>
        /// release the lock and return the protected resource to vault for use by others
        /// </summary>
        [NoDirectInvoke]
        public void Dispose()
        {
            if (_flag?.TrySet() == true)
            {
                var b = _box;
                // ReSharper disable once RedundantAssignment DEBUG vs RELEASE
                var temp = _disposeMethod(_vault, b, Mode);
                Debug.Assert(temp == null);
            }
        }

        /// <summary>
        ///  Obtain a writable locked resource.  Keep attempting until
        ///  sooner of following occurs:
        ///     1- time period specified by <paramref name="timeout"/> expires or
        ///     2- cancellation is requested via <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <param name="timeout">the max time to wait for</param>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
        /// <exception cref="ArgumentOutOfRangeException">Non-positive <paramref name="timeout"/></exception>
        /// <exception cref="TimeoutException">Could not obtain write lock in time specified by <paramref name="timeout"/></exception>
        /// <exception cref="OperationCanceledException">A cancellation request was propagated to the <paramref name="token"/></exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public ValListLockedResource<TVault, TItem> Lock(TimeSpan timeout, CancellationToken token)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"");
            return UpgradeAction(timeout, token);
        }

        /// <summary>
        /// Get a writable lock.
        /// </summary>
        /// <param name="timeout">how long to keep attempting before throwing <see cref="TimeoutException"/></param>
        /// <returns>the resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
        /// <exception cref="ArgumentOutOfRangeException">Non-positive <paramref name="timeout"/></exception>
        /// <exception cref="TimeoutException">Could not obtain write lock in time specified by <paramref name="timeout"/></exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public ValListLockedResource<TVault, TItem> Lock(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"");
            return UpgradeAction(timeout, CancellationToken.None);
        }


        /// <summary>
        ///  Obtain a writable locked resource. 
        /// </summary>
        /// <param name="token">a token to which cancellation requests can be propagated.</param>
        /// <returns>the resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
        /// <exception cref="OperationCanceledException">A cancellation request was propagated to the <paramref name="token"/></exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public ValListLockedResource<TVault, TItem> Lock(CancellationToken token) => UpgradeAction(null, token);

        /// <summary>
        /// Obtain the writable locked resource
        /// </summary>
        /// <returns>the locked resource</returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been initialized
        /// validly</exception>
        /// <exception cref="TimeoutException">Unable to obtain locked resource in the underlying vault's
        /// <see cref="DefaultTimeout"/>.</exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
        [return: UsingMandatory]
        public ValListLockedResource<TVault, TItem> Lock() => UpgradeAction(_vault.DefaultTimeout, CancellationToken.None);

        /// <summary>
        /// Wait to obtain the WriteLock potentially forever
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">This locked resource object has not been </exception>
        /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock.</exception>
        [return: UsingMandatory]
        public ValListLockedResource<TVault, TItem> LockWaitForever() => UpgradeWaitForever();
        #endregion

        #region Public ReadOnlyList-Related Methods
        /// <summary>
        /// Get a string representation of the protected resource
        /// </summary>
        /// <returns>A string representation</returns>
        public new string ToString() => $"{typeof(BigValueList<TItem>.BigValListReadOnlyView).Name} -- Count: {_wrapped.Count}";

        /// <summary>
        /// Get an enumerator to enumerate the collection
        /// </summary>
        /// <returns>an enumerator</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByRefList<TItem>.Enumerator GetEnumerator() => _wrapped.GetEnumerator();
        /// <summary>
        /// Get an enumerator with a filter such that it enumerates only items of shich the predicate is true.
        /// </summary>
        /// <param name="filter">The predicate filter to apply while enumerating.</param>
        /// <returns>An enumerator that enumerates items that satisfy the filtering predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filter"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByRefList<TItem>.WhereEnumerator GetFilteredEnumerator([NotNull] RefPredicate<TItem> filter) => _wrapped.GetFilteredEnumerator(filter);
        /// <summary>
        /// Get an enumerator that enumerates the items in this list and applies a transformation to them,
        /// returning a value of the type specified by <typeparamref name="TTargetType"/> (which must be vault-safe).
        /// </summary>
        /// <typeparam name="TTargetType">vault safe type returned</typeparam>
        /// <param name="transformer">the transforming function</param>
        /// <returns>A transforming enumerator to enumerate the collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transformer"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByRefList<TItem>.SelectEnumerator<TTargetType> GetTransformingEnumerator<[VaultSafeTypeParam] TTargetType>(RefFunc<TItem, TTargetType> transformer) => _wrapped.GetTransformingEnumerator(transformer);
        /// <summary>
        /// Get a filtered transforming enumerator that uses the supplied transformer function <paramref name="transformer"/> on those elements of which the supplied predicate <paramref name="filter"/> is true.
        /// </summary>
        /// <typeparam name="TTargetType">The return type, must be vault safe</typeparam>
        /// <param name="filter">The filter to select which items are enumerated</param>
        /// <param name="transformer">a transformer to produce the enumerated result</param>
        /// <returns>A filtering, transforming enumerator</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="transformer"/> were null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByRefList<TItem>.SelectWhereEnumerator<TTargetType> GetFilteredTransformingEnumerator<TTargetType>(RefPredicate<TItem> filter, RefFunc<TItem, TTargetType> transformer) => _wrapped.GetFilteredTransformingEnumerator(filter, transformer);
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
        /// <exception cref="BadComparerException{TComparer,TItem}">Supplied comparer was not initialized properly and comparers of type <typeparamref name="TComparer"/> do not work properly when not properly initialized.</exception>
        /// <remarks>Uses a utility optimized for comparisons of large value types.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BinarySearch<TComparer>(int index, int count, in TItem item, in TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TItem> => _wrapped.BinarySearch(index, count, in item, comparer);
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
        /// <exception cref="BadComparerException{TComparer,TItem}">Supplied comparer was not initialized properly and comparers of type <typeparamref name="TComparer"/> do not work properly when not properly initialized.</exception>
        /// <remarks>Uses a utility optimized for comparisons of large value types.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BinarySearch<TComparer>(in TItem item, in TComparer comparer) where TComparer : struct, IByRefCompleteComparer<TItem> => _wrapped.BinarySearch(in item, in comparer);
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
        public BigValueList<TItem> GetRange(int index, int count) => _wrapped.GetRange(index, count);

        /// <summary>
        /// Query whether all elements in this collection satisfy the predicate supplied by
        /// <paramref name="predicate"/>
        /// </summary>
        /// <param name="predicate">The predicate</param>
        /// <returns>True if the collection is empty or if every element in the collection satisfied <paramref name="predicate"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool All([NotNull] RefPredicate<TItem> predicate) => _wrapped.All(predicate);
        /// <summary>
        /// Query whether at least one item in the collection satisfies the supplied predicate.
        /// </summary>
        /// <param name="predicate">The predicate</param>
        /// <returns>True if at least one item in the collection satisfies the predicate, false otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Any([NotNull] RefPredicate<TItem> predicate) => _wrapped.Any(predicate);
        /// <summary>
        /// Return the first item in the collection that satisfies the given predicate.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The first item in the collection that satisfies <paramref name="predicate"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">No item in the collection satisfied the predicate (or collection empty).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem First([NotNull] RefPredicate<TItem> predicate) => ref _wrapped.First(predicate);
        /// <summary>
        /// Return the first element in the collection that satisfies the predicate or return the
        /// default value of TItem.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The first item that satisfied the predicate or the default value of TItem if no item satisfies the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem FirstOrDefault([NotNull] RefPredicate<TItem> predicate) => ref _wrapped.FirstOrDefault(predicate);
        /// <summary>
        /// Return the last element in the collection that satisfies the predicate.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The last item that satisfied the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">Collection contained no matching value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem Last([NotNull] RefPredicate<TItem> predicate) => ref _wrapped.Last(predicate);
        /// <summary>
        /// Return the last element in the collection that satisfies the predicate or return the
        /// default value of TItem.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The last item that satisfied the predicate or the default value of TItem if no item satisfies the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem LastOrDefault([NotNull] RefPredicate<TItem> predicate) => ref _wrapped.LastOrDefault(predicate);
        /// <summary>
        /// Return the sole item in the collection that satisfies the predicate.
        /// </summary>
        /// <param name="predicate">the predicate</param>
        /// <returns>The only item in the collection that satisfies the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">The number of items in the collection that satisfy the collection was not EXACTLY ONE.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem Single([NotNull] RefPredicate<TItem> predicate) => ref _wrapped.Single(predicate);
        /// <summary>
        /// Return the sole item in the collection that satisfies the predicate or the default value of TItem if zero items
        /// in the collection satisfy it.
        /// </summary>
        /// <param name="predicate">The predicate</param>
        /// <returns>The sole item in the collection that satisfies the predicate, if any, otherwise the default value of TItem</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">More than one item in the collection satisfies the predicate.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem SingleOrDefault([NotNull] RefPredicate<TItem> predicate) => ref _wrapped.SingleOrDefault(predicate);
        /// <summary>
        /// Return the first element in the collection.
        /// </summary>
        /// <returns>The first item</returns>
        /// <exception cref="InvalidOperationException">The collection is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem First() => ref _wrapped.First();
        /// <summary>
        /// Get the first item in the collection.
        /// </summary>
        /// <returns>The first item in the collection if any, otherwise default value of TItem </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem FirstOrDefault() => ref _wrapped.FirstOrDefault();
        /// <summary>
        /// Return the last element in the collection.
        /// </summary>
        /// <returns>The last item</returns>
        /// <exception cref="InvalidOperationException">The collection is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem Last() => ref _wrapped.Last();
        /// <summary>
        /// Get the last item in the collection.
        /// </summary>
        /// <returns>The last item in the collection if any, otherwise default value of TItem </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem LastOrDefault() => ref _wrapped.LastOrDefault();
        /// <summary>
        /// Get the sole item in the collection.
        /// </summary>
        /// <returns>The sole item in the collection.</returns>
        /// <exception cref="InvalidOperationException">The collection did not contain EXACTLY one element.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem Single() => ref _wrapped.Single();
        /// <summary>
        /// Get the sole item in the collection or -- if empty -- the default value of TItem
        /// </summary>
        /// <returns>the sole item in the collection or -- if empty -- the default value of TItem</returns>
        /// <exception cref="InvalidOperationException">There was more than one element in the collection.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TItem SingleOrDefault() => ref _wrapped.SingleOrDefault();
        /// <summary>
        /// Search the collection using a default initialized <typeparamref name="TComparer"/>
        /// to determine whether the collection contains the specified <paramref name="item"/>.
        /// </summary>
        /// <typeparam name="TComparer">The type of the comparer to use. It should work properly when default initialized.</typeparam>
        /// <param name="item">the item to find</param>
        /// <returns>true if the item is found, false otherwise.</returns>
        /// <exception cref="BadComparerException{TComparer,TItem}"> <typeparamref name="TComparer"/> does not work properly when default-initialized</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains<TComparer>(in TItem item) where TComparer : struct, IByRefCompleteComparer<TItem> => _wrapped.Contains<TComparer>(in item);
        /// <summary>
        /// Search the collection using the specified <paramref name="comparer"/> to determine if the collection contains
        /// the specified <paramref name="item"/>
        /// </summary>
        /// <typeparam name="TComparer">The type of the comparer to use.</typeparam>
        /// <param name="item">the item to find</param>
        /// <param name="comparer">the comparer to use</param>
        /// <returns>true if the item is found, false otherwise.</returns>
        /// <exception cref="BadComparerException{TComparer,TItem}"><paramref name="comparer"/>
        /// was not properly initialized and requires proper initialization to work.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains<TComparer>(in TItem item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TItem> =>
            _wrapped.Contains(in item, in comparer);
        /// <summary>
        /// Find every item in this collection that satisfied the supplied predicate and return
        /// and immutable array of such items.
        /// </summary>
        /// <param name="match">The predicate.</param>
        /// <returns>An immutable array of all items in the collection that satisfy the predicate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableArray<TItem> FindAll([NotNull] RefPredicate<TItem> match) => _wrapped.FindAll(match);
        /// <summary>
        /// Find the index of the first item in the collection that satisfies the predicate supplied
        /// by <paramref name="match"/>.
        /// </summary>
        /// <param name="match">The predicate to supply.</param>
        /// <returns>The index of the first item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FindIndex([NotNull] RefPredicate<TItem> match) => _wrapped.FindIndex(match);
        /// <summary>
        /// Starting with index specified by <paramref name="startIndex"/>, find the index of the first item that matches the specified predicate.
        /// </summary>
        /// <param name="startIndex">the starting index</param>
        /// <param name="match">the predicate</param>
        /// <returns>The first index at or after <paramref name="startIndex"/> where the item satisfied <paramref name="match"/>; a negative number if no such element.</returns>
        ///  <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FindIndex(int startIndex, [NotNull] RefPredicate<TItem> match) => _wrapped.FindIndex(startIndex, match);
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
        public int FindIndex(int startIndex, int count, [NotNull] RefPredicate<TItem> match) => _wrapped.FindIndex(startIndex, count, match);
        /// <summary>
        /// Find the index of the last item in the collection that satisfies the predicate supplied
        /// by <paramref name="match"/>.
        /// </summary>
        /// <param name="match">The predicate to supply.</param>
        /// <returns>The index of the last item that satisfies <paramref name="match"/> or a negative number if no such value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FindLastIndex([NotNull] RefPredicate<TItem> match) => _wrapped.FindLastIndex(match);
        /// <summary>
        /// Find the index of the last item (starting with <paramref name="startIndex"/> and working backwards) that satisfies the predicate.
        /// </summary>
        /// <param name="startIndex">The index to begin from (working backwards).</param>
        /// <param name="match">The predicate</param>
        /// <returns>The index of the last item that satisfied <paramref name="match"/> or a negative number if no such.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is not within the bounds of the array.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FindLastIndex(int startIndex, RefPredicate<TItem> match) => _wrapped.FindLastIndex(startIndex, match);
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
        public int FindLastIndex(int startIndex, int count, RefPredicate<TItem> match) => _wrapped.FindLastIndex(startIndex, count, match);
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
        /// <exception cref="BadComparerException{TComparer,TItem}"><typeparamref name="TComparer"/> does not work properly when default initialized.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf<TComparer>(in TItem item) where TComparer : struct, IByRefCompleteComparer<TItem> => _wrapped.IndexOf<TComparer>(in item);

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
        /// <exception cref="BadComparerException{TComparer,TItem}"><typeparamref name="TComparer"/>
        /// was not properly initialized and does not work properly unless so initialized.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf<TComparer>(in TItem item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TItem> =>
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
        /// <exception cref="BadComparerException{TComparer,TItem}">comparer is not of a type that works properly when default initialized.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf<TComparer>(in TItem item, int startingIndex, int count)
            where TComparer : struct, IByRefCompleteComparer<TItem> =>
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
        /// <exception cref="BadComparerException{TComparer,TItem}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf<TComparer>(in TItem item, int startingIndex, int count, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TItem> =>
            _wrapped.IndexOf(in item, startingIndex, count, comparer);

        /// <summary>
        /// Find the last index of the specified item in a sub-range of this collection.
        /// </summary>
        /// <typeparam name="TComparer">The comparer type</typeparam>
        /// <param name="item">the item you seek</param>
        /// <param name="comparer">the comparer to use</param>
        /// <returns>the index of the last occurence of <paramref name="item"/> in collection; a negative
        /// number if not found.</returns>
        /// <exception cref="BadComparerException{TComparer,TItem}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOf<TComparer>(in TItem item, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TItem> =>
            _wrapped.LastIndexOf(in item, comparer);

        /// <summary>
        /// Find the last index of the specified item in the collection.
        /// </summary>
        /// <typeparam name="TComparer">The type of comparer, which will be default initialized.</typeparam>
        /// <param name="item">the item whose index you want to find.</param>
        /// <returns>the index of the last occurence of <paramref name="item"/> in the collection or a negative number
        /// if not found.</returns>
        /// <exception cref="BadComparerException{TComparer,TItem}">comparer is not of a type that works when default initialized
        /// and has not been properly initialized.</exception>
        /// <remarks>May be faster especially for large value types.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOf<TComparer>(in TItem item)
            where TComparer : struct, IByRefCompleteComparer<TItem> => _wrapped.LastIndexOf<TComparer>(in item);

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
        /// <exception cref="BadComparerException{TComparer,TItem}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOf<TComparer>(in TItem item, int startingIndex, int count)
            where TComparer : struct, IByRefCompleteComparer<TItem> =>
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
        /// <exception cref="BadComparerException{TComparer,TItem}">comparer is not of a type that works when default initialized and has not been properly initialized.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOf<TComparer>(in TItem item, int startingIndex, int count, in TComparer comparer)
            where TComparer : struct, IByRefCompleteComparer<TItem> =>
            _wrapped.LastIndexOf(in item, startingIndex, count, comparer);

        /// <summary>
        /// Copy the collection to an immutable array.
        /// </summary>
        /// <returns>An immutable array that contains a copy of all the elements herein.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableArray<TItem> ToArray() => _wrapped.ToArray();
        #endregion

        #region Private Methods
        private ValListLockedResource<TVault, TItem> UpgradeAction(TimeSpan? ts, CancellationToken token)
        {
            if (_box == null || !IsGood) throw new InvalidOperationException("This object is invalid.");
            if (ts.HasValue && ts <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ts), ts, @"Not null timespan must have positive value.");
            _upgradeAction(ts, token);
            return ValListLockedResource<TVault, TItem>.CreateWritableLockedResource(_vault, _box);
        }

        private ValListLockedResource<TVault, TItem> UpgradeWaitForever()
        {
            if (_box == null || !IsGood) throw new InvalidOperationException("This object is invalid.");
            _upgradeForeverAction();
            return ValListLockedResource<TVault, TItem>.CreateWritableLockedResource(_vault, _box);
        } 
        #endregion

        #region Privates
        private readonly DisposeFlag _flag;
        private readonly Func<ReadWriteListVault<TItem, BigValueList<TItem>>, Vault<BigValueList<TItem>>.Box, AcquisitionMode, Vault<BigValueList<TItem>>.Box> _disposeMethod;
        private readonly Vault<BigValueList<TItem>>.Box _box;
        private readonly TVault _vault;
        private readonly BigValueList<TItem>.BigValListReadOnlyView _wrapped;
        private const AcquisitionMode Mode = AcquisitionMode.UpgradableReadOnly;
        private readonly Action<TimeSpan?, CancellationToken> _upgradeAction;
        private readonly Action _upgradeForeverAction;
        #endregion
    }
}