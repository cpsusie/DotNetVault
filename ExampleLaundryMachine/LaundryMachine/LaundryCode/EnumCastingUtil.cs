using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public static unsafe class EnumCastingUtil<TEnum, TBacker> where TEnum : unmanaged, Enum where TBacker : unmanaged, IEquatable<TBacker>, IComparable<TBacker>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static TEnum CastToEnum(TBacker backer) => *((TEnum*) (&backer));
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static TBacker CastToBacker(TEnum enumVal) => *((TBacker*) (&enumVal));
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static TEnum[] CastToEnum([NotNull] TBacker[] arr)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            return (TEnum[]) (object) arr;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static TBacker[] CastToBacker([NotNull] TEnum[] arr)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            return (TBacker[]) (object) arr;
        }

        public static TEnum[] SortAndDeduplicate([NotNull] IEnumerable<TEnum> input)
        {
            TEnum[] ret;
            if (input == null) throw new ArgumentNullException(nameof(input));
            var origArr = input.ToArray();
            Sort(origArr);
            int newLength = DoDestructiveInPlaceDeduplicationOnPresortedArray(origArr, origArr.Length);
            if (newLength < origArr.Length)
            {
                ret = new TEnum[newLength];
                for (int i = 0; i < newLength; ++i)
                {
                    ret[i] = origArr[i];
                }
            }
            else
            {
                ret = origArr;
            }
            ValidateNoDupes(ret);
            Debug.Assert(ret != null && ret.Length == newLength);
            return ret;
        }

        [Conditional("DEBUG")]
        private static void ValidateNoDupes(TEnum[] arr)
        {
            if (new HashSet<TEnum>(arr).Count != arr.Length)
            {
                throw new ApplicationException("Dedupe routine needs to be debugged.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void Sort([NotNull] TEnum[] arr)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            TBacker[] backer = CastToBacker(arr);
            Array.Sort(backer);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static int BinarySearch([NotNull] TEnum[] arr, TEnum findMe)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            TBacker[] backArr = CastToBacker(arr);
            return Array.BinarySearch(backArr, CastToBacker(findMe));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static int BinarySearch([NotNull] TBacker[] backer, TEnum findMe)
        {
            if (backer == null) throw new ArgumentNullException(nameof(backer));
            return Array.BinarySearch(backer, CastToBacker(findMe));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static int IndexOf([NotNull] TEnum[] arr, TEnum findMe)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            return Array.IndexOf(CastToBacker(arr), CastToBacker(findMe));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static int IndexOf([NotNull] TBacker[] arr, TEnum findMe)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            return Array.IndexOf(arr, CastToBacker(findMe));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool Contains([NotNull] TEnum[] arr, TEnum findMe)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            return IndexOf(arr, findMe) > -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool Contains([NotNull] TBacker[] arr, TEnum findMe)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            return IndexOf(arr, findMe) > -1;
        }

        private static int DoDestructiveInPlaceDeduplicationOnPresortedArray([NotNull] TEnum[] input, int length)
        {
            TBacker[] arr = CastToBacker(input);
            Debug.Assert(arr != null && length > -1);
            int resultingLength;
            switch (length)
            {
                case 0:
                case 1:
                    resultingLength = length;
                    break;
                case 2:
                    resultingLength = arr[0].Equals(arr[1]) ? 1 : 2;
                    break;
                default:
                    int index = 1;
                    for (int i = 1; i < length; i++)
                    {
                        if (!arr[i].Equals( arr[i - 1]))
                            arr[index++] = arr[i];
                    }
                    resultingLength = index;
                    break;
            }
            return resultingLength;
        }

        static EnumCastingUtil()
        {
            var actualBackingTypeOfTEnum = Enum.GetUnderlyingType(typeof(TEnum));
            var expectedBackingTypeOfTEnum = typeof(TBacker);
            if (actualBackingTypeOfTEnum != expectedBackingTypeOfTEnum)
            {
                throw new InvalidCastException(
                    $"The enum type [{typeof(TEnum).Name}] is an enumeration whose backing type is [{actualBackingTypeOfTEnum.Name}].  " +
                    $"The backing type specified by {nameof(TBacker)} is [{typeof(TBacker).Name}] used " +
                    $"by [{typeof(EnumCastingUtil<,>).Name}] does not match and is invalid.");
            }
        }
    }
}