using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class RangeException<TRange, TVal> : ArgumentOutOfRangeException where TVal : unmanaged, IComparable<TVal>, IEquatable<TVal> where TRange : IRange<TVal>
    {

        public TVal OffendingVal => _offendingVal;
        [NotNull] public TRange Range => _range;

        public RangeException([NotNull] string paramName, TVal actualValue, [NotNull] TRange range) : base(
            paramName ?? throw new ArgumentNullException(nameof(paramName)), actualValue,
            MakeMessage(range ?? throw new ArgumentNullException(nameof(range)), actualValue))
        {
            _offendingVal = actualValue;
            _range = range;
        }

        private static string MakeMessage(TRange r, TVal v) =>
            $"The value [{v.ToString()}] must be greater than or equal to [{r.Min.ToString()}] and less than or equal to [{r.Max.ToString()}].";

        private readonly TVal _offendingVal;
        [NotNull] private readonly TRange _range;
    }
}
