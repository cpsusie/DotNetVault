using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using DotNetVault.Attributes;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public readonly struct LaundrySimulationResults : IEquatable<LaundrySimulationResults>, IComparable<LaundrySimulationResults>
    {
        public static ref readonly LaundrySimulationResults InitialValue => ref TheInitialVal;

        public TimeSpan FinalElapsedTime => TicksAtEnd.HasValue && TicksAtStart.HasValue
            ? TimeSpan.FromTicks(TicksAtEnd.Value - TicksAtStart.Value)
            : TimeSpan.Zero;

        public TimeSpan TimeSinceStarted => TicksAtStart.HasValue
            ? TimeSpan.FromTicks(Stopwatch.GetTimestamp() - TicksAtStart.Value)
            : TimeSpan.Zero;

        public bool NotStartedYet => TicksAtStart == null;
        public bool TerminatedBeforeFinish => LaundryItemsToGo > 0 && TicksAtStart != null && TicksAtEnd != null;
        public bool InProgress => !Finished && !TerminatedBeforeFinish;
        public bool Finished => LaundryItemsToGo < 1 && TicksAtStart != null && TicksAtEnd != null;
        public long? TicksAtStart { get; }
        public long? TicksAtEnd { get; }
        public int LaundryItemsToGo { get; }
        
        [Pure]
        public LaundrySimulationResults Start(int startWithItems)
        {
            if (TicksAtStart != null || TicksAtEnd != null || startWithItems < 1) throw new InvalidOperationException("Simulation already began.");
            return new LaundrySimulationResults(Stopwatch.GetTimestamp(), null, startWithItems);
        }
        [Pure]
        public LaundrySimulationResults AnotherItemCleaned()
        {
            long potentialClosingTs = Stopwatch.GetTimestamp();
            if (TicksAtStart == null || TicksAtEnd != null || LaundryItemsToGo < 1) throw new InvalidOperationException($"Cannot call {nameof(AnotherItemCleaned)} in state {ToString()}.");
            return new LaundrySimulationResults(TicksAtStart.Value, LaundryItemsToGo -1 < 1 ? (long?) potentialClosingTs : null, LaundryItemsToGo-1);
        }

        [Pure]
        public LaundrySimulationResults TerminateEarly()
        {
            long ts = Stopwatch.GetTimestamp();
            if (TicksAtStart == null || TicksAtEnd != null) throw new InvalidOperationException($"Cannot call {nameof(TerminateEarly)} in state {ToString()}.");
            return new LaundrySimulationResults(TicksAtStart.Value, ts, LaundryItemsToGo);
        }

        public static bool operator >(in LaundrySimulationResults lhs, in LaundrySimulationResults rhs) =>
            Compare(in lhs, in rhs) > 0;
        public static bool operator <(in LaundrySimulationResults lhs, in LaundrySimulationResults rhs) =>
            Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in LaundrySimulationResults lhs, in LaundrySimulationResults rhs) =>
            !(lhs < rhs);
        public static bool operator <=(in LaundrySimulationResults lhs, in LaundrySimulationResults rhs) =>
            !(lhs > rhs);
        public static bool operator ==(in LaundrySimulationResults lhs, in LaundrySimulationResults rhs) =>
            lhs.TicksAtStart == rhs.TicksAtStart && lhs.TicksAtEnd == rhs.TicksAtEnd &&
            lhs.LaundryItemsToGo == rhs.LaundryItemsToGo;
        public static bool operator !=(LaundrySimulationResults lhs, LaundrySimulationResults rhs) => !(lhs == rhs);
        public bool Equals(LaundrySimulationResults other) => other == this;
        public override bool Equals(object other) => (other as LaundrySimulationResults? == this);
        public int CompareTo(LaundrySimulationResults other) => Compare(in this, in other);

        public override int GetHashCode()
        {
            int hash = TicksAtStart.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ TicksAtEnd.GetHashCode();
                hash = (hash * 397) ^ LaundryItemsToGo;
            }
            return hash;
        }

        public override string ToString()
        {
            if (this == InitialValue)
            {
                return $"[{nameof(LaundrySimulationResults)}] -- Simulation not yet started.";
            }
            if (InProgress)
            {
                return
                    $"[{nameof(LaundrySimulationResults)}]-- Simulation began {TimeSinceStarted.Milliseconds:F3} milliseconds ago with {LaundryItemsToGo.ToString()} items remaining.";
            }

            return TerminatedBeforeFinish
                ? $"[{nameof(LaundrySimulationResults)}]-- Simulation terminated after {FinalElapsedTime.Milliseconds:F3} milliseconds with {LaundryItemsToGo} items still pending."
                : $"[{nameof(LaundrySimulationResults)}]-- Simulation complete after {FinalElapsedTime.Milliseconds:F3} milliseconds.";
        }

        private LaundrySimulationResults(long? startTicks, long? endTicks, int itemsToGo)
        {
            TicksAtStart = startTicks;
            TicksAtEnd = endTicks;
            LaundryItemsToGo = itemsToGo;
        }

        private static int Compare(in LaundrySimulationResults lhs, in LaundrySimulationResults rhs)
        {
            int ret;
            int startRes = Comp(lhs.TicksAtStart, rhs.TicksAtStart);
            
            if (startRes == 0)
            {
                int endRes = Comp(lhs.TicksAtEnd, rhs.TicksAtEnd);
                ret = endRes != 0 ? endRes : lhs.LaundryItemsToGo.CompareTo(rhs.LaundryItemsToGo);
            }
            else
            {
                ret = startRes;
            }

            return ret;

            int  Comp(long? l, long? r)
            {
                if (l == r) return 0;
                if (l == null) return -1;
                if (r == null) return 1;
                return l.Value.CompareTo(r.Value);
            }
        }

        private static readonly LaundrySimulationResults TheInitialVal = default;
    }
}