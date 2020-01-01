using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public struct ArrayEnumerator<T> : IEnumerator<T>
    {
        public T Current => _current;

        object IEnumerator.Current => _idx > -1 && _idx < Arr.Length
            ? Current
            : throw new InvalidOperationException("Enumerator in invalid state to access Current property.");

        public ArrayEnumerator(T[] arr)
        {
            _arr = arr ?? throw new ArgumentNullException(nameof(arr));
            _current = default;
            _idx = -1;
        }

        public bool MoveNext()
        {
            ++_idx;
            if (_idx > -1 && _idx < Arr.Length)
            {
                _current = Arr[_idx];
                return true;
            }
            _current = default;
            return false;
        }

        public void Reset()
        {
            _idx = -1;
            _current = default;
        }
        
        void IDisposable.Dispose() { }

        
        private T[] Arr => _arr ?? Array.Empty<T>();
        private T _current;
        private int _idx;
        private readonly T[] _arr;
    }

    /// <summary>
    /// This is a higher level version of <see cref="EnumCompleteComparer{TEnum,TBacker}"/>
    /// that selects the correct backer for you.
    /// Supported backers are byte, sbyte, ushort, short, uint, int, ulong, long
    /// </summary>
    /// <typeparam name="TEnum">Must be a concrete unmanaged enum type</typeparam>
    [VaultSafe]
    public readonly struct EnumComparer<TEnum> : ICompleteComparer<TEnum> where TEnum : unmanaged, Enum
    {
        public bool Equals(TEnum x, TEnum y) => ComparerImpl.Equals(x, y);
        public int GetHashCode(TEnum obj) => ComparerImpl.GetHashCode(obj);
        public int Compare(TEnum x, TEnum y) => ComparerImpl.Compare(x, y);
        public int IndexOf([NotNull] TEnum[] arr, TEnum val) => ComparerImpl.BinarySearch(arr, val);
        public bool Contains([NotNull] TEnum[] arr, TEnum val) => IndexOf(arr ?? throw new ArgumentNullException(nameof(arr)), val) > -1;
        public void Sort([NotNull] TEnum[] arr) =>
            ComparerImpl.Sort(arr ?? throw new ArgumentNullException(nameof(arr)));
        public TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) =>
            ComparerImpl.SortAndDeduplicate(col ?? throw new ArgumentNullException(nameof(col)));
        public int BinarySearch(TEnum[] arr, TEnum val) =>
            ComparerImpl.BinarySearch(arr ?? throw new ArgumentNullException(nameof(arr)), val);

        #region Nested Types
        [VaultSafe(true)]
        internal abstract class InternalEnumCompleteComparer : IEqualityComparer<TEnum>, IComparer<TEnum>
        {
            public abstract bool Equals(TEnum lhs, TEnum rhs);
            public abstract int GetHashCode(TEnum obj);
            public abstract int Compare(TEnum lhs, TEnum rhs);
            public abstract int BinarySearch([NotNull] TEnum[] arr, TEnum val);
            public abstract void Sort([NotNull] TEnum[] arr);
            public abstract TEnum[] SortAndDeduplicate([NotNull] IEnumerable<TEnum> col);
        }
        [VaultSafe]
        internal sealed class IntBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);
            private readonly EnumCompleteComparer<TEnum, int> _theComparer = new EnumCompleteComparer<TEnum, int>();
        }

        private sealed class LongBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);

            private readonly EnumCompleteComparer<TEnum, long> _theComparer = new EnumCompleteComparer<TEnum, long>();
        }

        private sealed class ULongBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);

            private readonly EnumCompleteComparer<TEnum, ulong> _theComparer = new EnumCompleteComparer<TEnum, ulong>();
        }

        private sealed class UIntBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);

            private readonly EnumCompleteComparer<TEnum, uint> _theComparer = new EnumCompleteComparer<TEnum, uint>();
        }

        private sealed class ShortBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);


            private readonly EnumCompleteComparer<TEnum, short> _theComparer = new EnumCompleteComparer<TEnum, short>();
        }

        private sealed class UShortBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);

            private readonly EnumCompleteComparer<TEnum, ushort> _theComparer = new EnumCompleteComparer<TEnum, ushort>();
        }
        private sealed class ByteBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);

            private readonly EnumCompleteComparer<TEnum, byte> _theComparer = new EnumCompleteComparer<TEnum, byte>();
        }

        private sealed class SByteBasedEnumCompleteComparer : InternalEnumCompleteComparer
        {
            public override bool Equals(TEnum lhs, TEnum rhs) => _theComparer.Equals(lhs, rhs);
            public override int GetHashCode(TEnum obj) => _theComparer.GetHashCode(obj);
            public override int Compare(TEnum lhs, TEnum rhs) => _theComparer.Compare(lhs, rhs);
            public override int BinarySearch(TEnum[] arr, TEnum val) => _theComparer.BinarySearch(arr, val);
            public override void Sort(TEnum[] arr) => _theComparer.Sort(arr);
            public override TEnum[] SortAndDeduplicate(IEnumerable<TEnum> col) => _theComparer.SortAndDedupe(col);

            private readonly EnumCompleteComparer<TEnum, sbyte> _theComparer = new EnumCompleteComparer<TEnum, sbyte>();
        }

        #endregion
        static EnumComparer()
        {
            TheInternalComparerType = new LocklessWriteOnce<InternalEnumCompleteComparer>();
            TheInternalComparerType.SetOrThrow(GetBacker());
            Debug.Assert(TheInternalComparerType.IsSet && TheInternalComparerType.Value != null);
        }

        static InternalEnumCompleteComparer GetBacker()
        {
            InternalEnumCompleteComparer ret;
            switch (Type.GetTypeCode(typeof(TEnum)))
            {
                default:
                    throw new UnsupportedEnumTypeException(typeof(TEnum));
                case TypeCode.SByte:
                    ret= new SByteBasedEnumCompleteComparer();
                    break;
                case TypeCode.Byte:
                    ret = new ByteBasedEnumCompleteComparer();
                    break;
                case TypeCode.Int16:
                    ret = new ShortBasedEnumCompleteComparer();
                    break;
                case TypeCode.UInt16:
                    ret = new UShortBasedEnumCompleteComparer();
                    break;
                case TypeCode.Int32:
                    ret = new IntBasedEnumCompleteComparer();
                    break;
                case TypeCode.UInt32:
                    ret = new UIntBasedEnumCompleteComparer();
                    break;
                case TypeCode.Int64:
                    ret = new LongBasedEnumCompleteComparer();
                    break;
                case TypeCode.UInt64:
                    ret = new ULongBasedEnumCompleteComparer();
                    break;
            }
            return ret;
        }

        #region Private Data
        private InternalEnumCompleteComparer ComparerImpl => TheInternalComparerType.Value;
        private static readonly LocklessWriteOnce<InternalEnumCompleteComparer> TheInternalComparerType; 
        #endregion
    }
}