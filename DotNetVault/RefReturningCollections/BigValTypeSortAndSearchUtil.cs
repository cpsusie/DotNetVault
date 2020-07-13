using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using JetBrains.Annotations;

namespace DotNetVault.RefReturningCollections
{
    /// <summary>
    /// A delegate that evaluates two values for equality when the values are passed by constant reference
    /// </summary>
    /// <typeparam name="T">the value type</typeparam>
    /// <param name="lhs">left hand operand</param>
    /// <param name="rhs">right hand operand</param>
    /// <returns>true if they are equal values, false otherwise</returns>
    [NoNonVsCapture]
    public delegate bool EqualsByRef<[VaultSafeTypeParam] T>(in T lhs, in T rhs);
    /// <summary>
    /// Test the left-hand value to see if it is less than the right hand value
    /// </summary>
    /// <param name="lhs">the left hand value</param>
    /// <param name="rhs">the right hand value</param>
    /// <typeparam name="T">the type of the value</typeparam>
    /// <returns>True if the left-hand value is less than the right hand value, false otherwise</returns>
    [NoNonVsCapture]
    public delegate bool LessThanByRef<[VaultSafeTypeParam] T>(in T lhs, in T rhs);
    /// <summary>
    /// Gets a value-based hash code from the specified object
    /// </summary>
    /// <param name="obj">the object</param>
    /// <typeparam name="T">the type of the object</typeparam>
    /// <returns>a hash code calculated from the value of the object.</returns>
    [NoNonVsCapture]
    public delegate int HashByRef<[VaultSafeTypeParam] T>(in T obj);

    /// <summary>
    /// This interface should be implemented by unmanaged value types.
    /// It permits equals/not equals comparisons as well as full-ordering comparisons.
    /// It also will retrieve a hash code.
    /// These comparers should be more efficient than the standard comparers for large value types.
    ///
    /// For smaller value types, using the built in .NET comparers is probably best.  
    /// </summary>
    /// <typeparam name="T">the type</typeparam>
    /// <remarks>The results between Equals and Compare must always be consistent as code in this library
    /// makes that assumption.  Also, if two objects are Equal or Compare to zero, they should ALWAYS (at least
    /// for process-lifetime, not necessarily in different processes or across runs) return the same integer.  Of course,
    /// since <typeparamref name="T"/> can potentially have many more possible values than <see cref="int"/>, two objects
    /// that hash the same does not guarantee that they are equal.  If they are equal however they MUST hash the same.
    /// </remarks>
    public interface IByRefCompleteComparer<[VaultSafeTypeParam] T> : IEqualityComparer<T>, IComparer<T> where T : struct, IEquatable<T>, IComparable<T>
    {
        
        /// <summary>
        /// True if this value was properly constructed, false otherwise.
        /// </summary>
        bool IsValid { get; }
        /// <summary>
        /// True if this type works correctly when default constructed.
        /// </summary>
        bool WorksCorrectlyWhenDefaultConstructed { get; }
        /// <summary>
        /// Test two values to see if they have the same value
        /// </summary>
        /// <param name="lhs">the left-hand operand</param>
        /// <param name="rhs">the right-hand operand</param>
        /// <returns>true if the lefthand operand and righthand operand have the same value,
        /// false otherwise</returns>
        /// <remarks>If two values are equal according to this method, than they must compare to zero when using
        /// the <see cref="Compare"/> method.  If they are not equal according to this method, they must NOT
        /// compare to zero according to the <see cref="Compare"/> method. Also, if they are equal according to this method,
        /// <see cref="GetHashCode"/> must return the same value for both of them (though there is no hard requirement that if they are
        /// not equal they must produce different hash codes -- though this is desirable to the extent possible)</remarks>
        bool Equals(in T lhs, in T rhs);
        /// <summary>
        /// Compute a hash code based on the value of <see paramref="obj"/>.  You must ensure
        /// that two values being equal according to this comparer guarantees that their hash
        /// is the same.
        /// </summary>
        /// <param name="obj">the value whose hash you want to calculate</param>
        /// <returns>a hash code</returns>
        int GetHashCode(in T obj);
        /// <summary>
        /// Compare to values to establish their relative ordering
        /// </summary>
        /// <param name="lhs">the left hand operand</param>
        /// <param name="rhs">the right hand operand</param>
        /// <returns>a negative number if <paramref name="lhs"/> is less than <paramref name="rhs"/>, 0 if <paramref name="lhs"/> is equal to <paramref name="rhs"/> and
        /// a positive number if <paramref name="lhs"/> is greater than <paramref name="rhs"/>.</returns>
        /// <remarks>If <paramref name="lhs"/> and <paramref name="rhs"/> compare to zero, <see cref="Equals"/> when called on them must return true and <see cref="GetHashCode"/> must
        /// return the same value for each of them.  If they do not compare to zero, then <see cref="Equals"/> MUST return false.  If they compare to zero, <see cref="GetHashCode"/>
        /// MUST return the same value -- though it need not always return different values if they are not equal</remarks>
        int Compare(in T lhs, in T rhs);
    }

    /// <inheritdoc />
    [VaultSafe(true)]
    public readonly struct RefComparerFromDelegates<[VaultSafeTypeParam] T> : IByRefCompleteComparer<T> where T : struct, IEquatable<T>, IComparable<T>
    {
        /// <summary>
        /// Create a ref compare from a set of delegates
        /// </summary>
        /// <param name="equals">A delegate that tests for equality</param>
        /// <param name="lessThan">A delegate that tests if left hand operand is less than right hand operand</param>
        /// <param name="hash">A delegate that gets a hash code</param>
        /// <returns>A <see cref="RefComparerFromDelegates{T}"/> that uses the delegates to implement <see cref="IByRefCompleteComparer{T}"/>.  Make sure
        /// the delegates conform to the requirements described in that interfaces documentation.</returns>
        public static RefComparerFromDelegates<T> CreateRefComparer([NotNull] EqualsByRef<T> equals, [NotNull] LessThanByRef<T> lessThan,
            [NotNull] HashByRef<T> hash) => new RefComparerFromDelegates<T>(equals, lessThan, hash);

        //Template
        //public static RefComparer<T> CreateDefaultRefComparer()
        //    => return new RefComparer<T>((in T lhs, in T rhs) => lhs == rhs, (in T lhs, in T rhs) => lhs < rhs,
        //        (in T obj) => obj.GetHashCode());

        /// <summary>
        /// example use of template for <see cref="DateTime"/>
        /// </summary>
        public static RefComparerFromDelegates<DateTime> CreateDefaultDateTimeComparer()
            =>  new RefComparerFromDelegates<DateTime>((in DateTime lhs, in DateTime rhs) => lhs == rhs, (in DateTime lhs, in DateTime rhs) => lhs<rhs,
                (in DateTime obj) => obj.GetHashCode());

        /// <summary>
        /// example use of template for <see cref="TimeSpan"/>
        /// </summary>
        public static RefComparerFromDelegates<TimeSpan> CreateDefaultTimeSpanComparer()
            => new RefComparerFromDelegates<TimeSpan>((in TimeSpan lhs, in TimeSpan rhs) => lhs == rhs, (in TimeSpan lhs, in TimeSpan rhs) => lhs < rhs,
                (in TimeSpan obj) => obj.GetHashCode());

        /// <summary>
        /// True if this type has been properly initialized, false otherwise
        /// </summary>
        public bool IsValid => _equals != null && _lessThan != null && _hashByRef != null;

        /// <inheritdoc />
        public bool WorksCorrectlyWhenDefaultConstructed => false;

        /// <inheritdoc />
        /// <exception cref="NullReferenceException">This object has not been initialized.</exception>
        public bool Equals(in T lhs, in T rhs) => _equals(in lhs, in rhs);
        /// <inheritdoc />
        /// <exception cref="NullReferenceException">This object has not been initialized.</exception>
        public int GetHashCode(in T obj) => _hashByRef(in obj);
        /// <inheritdoc />
        /// <exception cref="NullReferenceException">This object has not been initialized.</exception>
        public int Compare(in T lhs, in T rhs)
        {
            if (_equals(in lhs, in rhs))
            {
                return 0;
            }
            return _lessThan(in lhs, in rhs) ? -1 : 1;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(T x, T y) => Equals(in x, in y);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(T obj) => GetHashCode(in obj);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y) => Compare(in x, in y);

        /// <summary>
        /// Initialize a comparer from three delegates
        /// </summary>
        /// <param name="equals">A delegate that tests for equality</param>
        /// <param name="lessThan">A delegate that tests if left hand operand is less than right hand operand</param>
        /// <param name="getHash">A delegate that gets a hash code</param>
        /// <exception cref="ArgumentNullException"><paramref name="equals"/>, <paramref name="lessThan"/> or <paramref name="getHash"/> was null.</exception>
        public RefComparerFromDelegates([NotNull] EqualsByRef<T> equals, [NotNull] LessThanByRef<T> lessThan, [NotNull] HashByRef<T> getHash)
        {
            _equals = equals ?? throw new ArgumentNullException(nameof(equals));
            _lessThan = lessThan ?? throw new ArgumentNullException(nameof(lessThan));
            _hashByRef = getHash ?? throw new ArgumentNullException(nameof(getHash));
        }


  

        private readonly EqualsByRef<T> _equals;
        private readonly LessThanByRef<T> _lessThan;
        private readonly HashByRef<T> _hashByRef;

    }
    //This can be used as a template for types that implement ==, < and GetHashCode in a way consistent with the requirements
    //of the IByRefCompleteComparer<T> interface. 
    //TEMPLATE
    //public readonly struct CustomByRefComparer<[VaultSafeTypeParam] T> : IByRefCompleteComparer<T>
    //    where T : struct, IComparable<T>, IEquatable<T>
    //{
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public bool Equals(in T lhs, in T rhs) => lhs == rhs;
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public int GetHashCode(in T obj) => obj.GetHashCode();
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public int Compare(in T lhs, in T rhs) => lhs == rhs ? 0 : (lhs < rhs ? -1 : 1);

    //    /// <inheritdoc />
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public bool Equals(T x, T y) => Equals(in x, in y);

    //    /// <inheritdoc />
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public int GetHashCode(T obj) => GetHashCode(in obj);

    //    /// <inheritdoc />
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public int Compare(T x, T y) => Compare(in x, in y);
    //}

    /// <summary>
    /// Example of template use: DateTime
    /// </summary>
    [VaultSafe]
    public readonly struct CustomDateTimeComparer : IByRefCompleteComparer<DateTime>
    {
        /// <summary>
        /// True if this type works correctly when default constructed.
        /// </summary>
        public bool WorksCorrectlyWhenDefaultConstructed => true;
        /// <summary>
        /// True if this type works correctly when default constructed.
        /// </summary>
        public bool IsValid => true;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(in DateTime lhs, in DateTime rhs) => lhs == rhs;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(in DateTime obj) => obj.GetHashCode();
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(in DateTime lhs, in DateTime rhs) => lhs == rhs ? 0 : (lhs < rhs ? -1 : 1);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DateTime x, DateTime y) => Equals(in x, in y);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(DateTime obj) => GetHashCode(in obj);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(DateTime x, DateTime y) => Compare(in x, in y);
    }

    /// <summary>
    /// Example of template use: TimeSpan
    /// </summary>
    [VaultSafe]
    public readonly struct CustomTimeSpanComparer : IByRefCompleteComparer<TimeSpan>
    {
        /// <summary>
        /// True if this type works correctly when default constructed.
        /// </summary>
        public bool WorksCorrectlyWhenDefaultConstructed => true;
        /// <summary>
        /// True if this type works correctly when default constructed.
        /// </summary>
        public bool IsValid => true;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(in TimeSpan lhs, in TimeSpan rhs) => lhs == rhs;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(in TimeSpan obj) => obj.GetHashCode();
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(in TimeSpan lhs, in TimeSpan rhs) => lhs == rhs ? 0 : (lhs < rhs ? -1 : 1);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TimeSpan x, TimeSpan y) => Equals(in x, in y);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(TimeSpan obj) => GetHashCode(in obj);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(TimeSpan x, TimeSpan y) => Compare(in x, in y);
    }

    internal readonly struct BigValTypeSortAndSearchUtil
    {

        public const int IntroSortThreshold = 16;
        public void Sort<[VaultSafeTypeParam] TComparand, TComparer>(TComparand[] arr, in TComparer comparer) 
            where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            if (arr == null)
            {
                throw new ArgumentNullException(nameof(arr));
            }

            TComparer copy = comparer;
            if (arr.Length < 2)
                return;
            DoIntrospectiveSort(arr, 0, arr.Length, ref copy);
        }


        public void Sort<[VaultSafeTypeParam] TComparand, TComparer>(TComparand[] arr, int length, in TComparer comparer)
            where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            if (length < 0) throw new ArgumentNotPositiveException<int>(nameof(length), length);
            if (length > arr.Length)
                throw new ArgumentOutOfRangeException(nameof(length), length,
                    @$"Parameter may not be greater than the length of the array.  Current length: [{arr.Length.ToString()}].");

            TComparer copy = comparer;
            if (arr.Length < 2)
                return;
            DoIntrospectiveSort(arr, 0, length, ref copy);
        }

        public int BinarySearch<[VaultSafeTypeParam] TComparand, TComparer>(TComparand[] arr, int index, int count, in TComparand findMe,
            in TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            if (arr == null)
            {
                throw new ArgumentNullException(nameof(arr));
            }
            if (index < 0) throw new ArgumentNegativeException<int>(nameof(index), index);
            if (count < 0) throw new ArgumentNegativeException<int>(nameof(count), count);
            if (index >= arr.Length) throw new ArgumentOutOfRangeException(nameof(index), index, 
                @$"Parameter must be less than the size of the array (value: {arr.Length}).");
            if (arr.Length - index < count)
                throw new ArgumentException(
                    $"Array length (value: {arr.Length}) - param {nameof(index)} " +
                    $"(value: {index}; value of difference: {arr.Length - index}) " +
                    $"must be greater than or equal to parameter {nameof(count)} (value: {count}).");
            TComparer copy = comparer;
            return ExecuteBinarySearch(arr, index, count, in findMe, ref copy);
        }

        //public int BinarySearch<[VaultSafeTypeParam] TComparand, TComparer>(TComparand[] arr, in TComparand findMe, in TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        //{
        //    if (arr == null)
        //    {
        //        throw new ArgumentNullException(nameof(arr));
        //    }
        //    TComparer copy = comparer;
        //    return ExecuteBinarySearch(arr, 0, arr.Length, in findMe, ref copy);
        //}

       

        private void DoIntrospectiveSort<[VaultSafeTypeParam] TComparand, TComparer>([NotNull] TComparand[] arr, int left, int arrLength, ref TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null && left >= 0 && arrLength >= 0);
            if (arrLength < 2)
                return;
            //DO NOT CONVERT TO ? : -- The default Comparer<T> is significantly faster that
            //the interface version thereof.  If the _comparer is null, that means we can use Comparer<T>.Default
            //for speed boost (not insignificant for large ops)
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            ExecuteIntroSort(arr, left, arrLength + left - 1, 2 * FloorLog2PlusOne(arrLength), ref comparer);
            

        }

        void ExecuteIntroSort<[VaultSafeTypeParam] TComparand, TComparer>([NotNull] TComparand[] arr, int lo, int hi, int depthLimit, ref TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null && lo >= 0);
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= IntroSortThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreater(arr, lo, hi, ref comparer);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreater(arr, lo, hi - 1, ref comparer);
                        SwapIfGreater(arr, lo, hi, ref comparer);
                        SwapIfGreater(arr, hi - 1, hi, ref comparer);
                        return;
                    }
                    ExecuteInsertionSort(arr, lo, hi, ref comparer);
                    return;
                }

                if (depthLimit == 0)
                {
                    ExecuteHeapSort(arr, lo, hi, ref comparer);
                    return;
                }
                depthLimit--;

                int p = PickAndPivotPartition(arr, lo, hi, ref comparer);
                ExecuteIntroSort(arr, p + 1, hi, depthLimit, ref comparer);
                hi = p - 1;
            }
        }

        private int ExecuteBinarySearch<[VaultSafeTypeParam] TComparand, TComparer>([NotNull] TComparand[] arr, int startingIndex, int arrayLength, in TComparand valueToFind, ref TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null && startingIndex >= 0 && arrayLength >= 0);
            int lo = startingIndex;
            int hi = startingIndex + arrayLength - 1;
            while (lo <= hi)
            {
                //we find the midpoint between lo and hi, then compare the value at the midpoint
                //with the value we are looking for.  If it is the value we want, we are done.
                //If it is less than the value we want then we set the new lo to one plus the midpoint 
                //If it is greater than the value we want then we set the new hi to one less than the midpoint
                //we continue until we find the item of lo becomes greater than hi (which means it was not found)
                int midPoint = lo + ((hi - lo) >> 1);
                int comparison = comparer.Compare(in arr[midPoint], in valueToFind);
                if (comparison == 0)
                    return midPoint;
                if (comparison < 0)
                    lo = midPoint + 1;
                else
                    hi = midPoint - 1;
            }
            //If not found, we return the bitwise complement of the lo limit 
            //of our final search range
            return ~lo;
        }

     
        

       
        private void ExecuteHeapSort<[VaultSafeTypeParam] TComparand, TComparer>(TComparand[] arr, int lo, int hi, ref TComparer comparer)
            where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null  && lo >= 0 && hi > lo);

            int n = hi - lo + 1;
            for (int i = n / 2; i >= 1; i = i - 1)
            {
                DownHeap(arr, i, n, lo, ref comparer);
            }
            for (int i = n; i > 1; i = i - 1)
            {
                Swap(arr, lo, lo + i - 1);
                DownHeap(arr, 1, i - 1, lo, ref comparer);
            }
        }

        private void ExecuteInsertionSort<[VaultSafeTypeParam] TComparand, TComparer>(TComparand[] arr, int lo, int hi, ref TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null);
            Debug.Assert(lo >= 0 && hi >= lo);

            int i, j;
            TComparand t;
            for (i = lo; i < hi; i++)
            {
                j = i;
                t = arr[i + 1];
                while (j >= lo && comparer.Compare(in t, in arr[j]) < 0)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = t;
            }
        }

        private void DownHeap<[VaultSafeTypeParam] TComparand, TComparer>([NotNull] TComparand[] arr, int i, int n, int lo, ref TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null &&  lo >= 0);
            TComparand d = arr[lo + i - 1];
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;
                if (child < n && comparer.Compare(in arr[lo + child - 1], in arr[lo + child]) < 0)
                {
                    child++;
                }
                if (!(comparer.Compare(in d, in arr[lo + child - 1]) < 0))
                    break;
                arr[lo + i - 1] = arr[lo + child - 1];
                i = child;
            }
            arr[lo + i - 1] = d;
        }

        private int PickAndPivotPartition<[VaultSafeTypeParam] TComparand, TComparer>([NotNull] TComparand[] arr, 
            int lo, int hi, ref TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null);
            Debug.Assert(lo >= 0 && hi > lo);

            int middle = lo + ((hi - lo) / 2);

            SwapIfGreater(arr, lo, middle, ref comparer);
            SwapIfGreater(arr, lo, hi, ref comparer);
            SwapIfGreater(arr, middle, hi, ref comparer);

            TComparand pivot = arr[middle];
            Swap(arr, middle, hi - 1);
            int left = lo,
                right = hi - 1;
            while (left < right)
            {
                // ReSharper disable EmptyEmbeddedStatement
                //INTENTIONAL Empty statement
                while (comparer.Compare(in arr[++left], in pivot) < 0) ;
                while (comparer.Compare(in pivot, in arr[--right]) < 0) ;
                // ReSharper restore EmptyEmbeddedStatement
                if (left >= right)
                    break;
                Swap(arr, left, right);
            }

            Swap(arr, left, (hi - 1));
            return left;
        }

        private void SwapIfGreater<[VaultSafeTypeParam] TComparand, TComparer>([NotNull] TComparand[] arr, int xIndex, int yIndex, ref TComparer comparer) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand> where TComparer : struct, IByRefCompleteComparer<TComparand>
        {
            Debug.Assert(arr != null);
            if (xIndex != yIndex)
            {
                if (comparer.Compare(in arr[xIndex], in arr[yIndex]) > 0)
                {
                    TComparand temp = arr[xIndex];
                    arr[xIndex] = arr[yIndex];
                    arr[yIndex] = temp;
                }
            }
        }

        private void Swap<TComparand>([NotNull] TComparand[] arr, int xIndex, int yIndex) where TComparand : struct, IEquatable<TComparand>, IComparable<TComparand>
        {
            Debug.Assert(arr != null);
            if (xIndex != yIndex)
            {
                TComparand temp = arr[xIndex];
                arr[xIndex] = arr[yIndex];
                arr[yIndex] = temp;
            }
        }

        private int FloorLog2PlusOne(int value)
        {
            Debug.Assert(value > 0);
            unchecked
            {
                uint x = (uint)value;
                x |= (x >> 1);
                x |= (x >> 2);
                x |= (x >> 4);
                x |= (x >> 8);
                x |= (x >> 16);

                uint temp = Ones32(x) - 1;
                return (int)(temp + 1);
            }
        }

        private uint Ones32(uint x)
        {
            x -= ((x >> 1) & 0x55555555);
            x = (((x >> 2) & 0x33333333) + (x & 0x33333333));
            x = (((x >> 4) + x) & 0x0f0f0f0f);
            x += (x >> 8);
            x += (x >> 16);
            return (x & 0x0000003f);
        }


    }
}