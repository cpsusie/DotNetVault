using System.Collections.Generic;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public interface ICompleteComparer<T> : IEqualityComparer<T>, IComparer<T>
    {
        int BinarySearch([NotNull] T[] arr, T val);
        void Sort([NotNull] T[] arr);
        T[] SortAndDeduplicate([NotNull] IEnumerable<T> col);
    }
}