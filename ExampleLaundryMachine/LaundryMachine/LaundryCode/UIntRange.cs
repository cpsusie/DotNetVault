using System;
using System.Collections;
using System.Collections.Generic;

namespace LaundryMachine.LaundryCode
{
    public readonly struct UIntRange : IRange<uint>, IEquatable<UIntRange>
    {
        #region Public Properties
        public ulong Count => (_max - _min) + 1;
        public uint Max => _max;
        public uint Min => _min; 
        #endregion

        #region CTORS
        public UIntRange(uint min, uint max)
        {
            if (max < min)
                throw new ArgumentException(
                    $"Parameter {nameof(max)} (value: {max.ToString()}) " +
                    $"cannot be less than parameter {nameof(min)} (value: {min.ToString()}).");
            _max = max;
            _min = min;
        }

        public UIntRange(uint singleVal) : this(singleVal, singleVal) { } 
        #endregion

        #region Public Methods and Operators
        public RangeEnumerator GetEnumerator() => RangeEnumerator.CreateRangeEnumerator(in this);
        public bool Contains(uint x) => x >= _min && x <= _max;
        public override string ToString() =>
            $"[{nameof(UIntRange)}] -- Min: {_min.ToString()}; Max: {_max.ToString()}; Count: {Count.ToString()}.";
        public static bool operator ==(in UIntRange lhs, in UIntRange rhs) =>
            lhs._min == rhs._min && lhs._max == rhs._max;
        public static bool operator !=(in UIntRange lhs, in UIntRange rhs) => !(lhs == rhs);
        public bool Equals(UIntRange other) => this == other;
        public override bool Equals(object other) => (other as UIntRange?) == this;

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)((_min * 397) ^ (_max * 389));
            }
        }

        public void Validate(string paramName, uint val)
        {
            if (!Contains(val))
            {
                throw new RangeException<UIntRange, uint>(paramName ?? "Unknown", val, this);
            }
        }
        #endregion

        #region Explicitly Implemented Methods
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        IEnumerator<uint> IEnumerable<uint>.GetEnumerator() => GetEnumerator(); 
        #endregion

        #region Nested Enumerator
        public struct RangeEnumerator : IEnumerator<uint>
        {
            public static RangeEnumerator CreateRangeEnumerator(in UIntRange r) => new RangeEnumerator(in r);

            public readonly uint Current => _current ?? 0;

            object IEnumerator.Current
            {
                get
                {
                    if (_current.HasValue)
                    {
                        return _current.Value;
                    }

                    throw new InvalidOperationException("The enumerator does not currently refer to any element.");
                }
            }


            private RangeEnumerator(in UIntRange r)
            {
                _current = null;
                _max = r.Max;
                _min = r.Min;
            }

            public bool MoveNext()
            {
                if (_current == null)
                {
                    _current = _min;
                    return true;
                }

                _current = _current.Value + 1;
                return _current <= _max;
            }

            public void Reset() => _current = null;

            public void Dispose() { }

            private uint? _current;
            private readonly uint _max;
            private readonly uint _min;
        }
        #endregion

        #region Private Data
        private readonly uint _max;
        private readonly uint _min; 
        #endregion
    }
}