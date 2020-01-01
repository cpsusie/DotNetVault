using System;
using System.Collections.Generic;

namespace LaundryMachine.LaundryCode
{
    public interface IRange<T> : IEnumerable<T> where T : unmanaged, IComparable<T>, IEquatable<T>
    {
        ulong Count { get; }
        public T Min { get; }
        public T Max { get; }
        bool Contains(T val);
        void Validate(string paramName, T val);
    }
}