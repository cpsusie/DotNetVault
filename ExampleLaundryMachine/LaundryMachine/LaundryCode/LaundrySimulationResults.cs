using System;
using System.Diagnostics.Contracts;
using DotNetVault.Attributes;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public readonly struct LaundrySimulationResults : IEquatable<LaundrySimulationResults>, IComparable<LaundrySimulationResults>
    {
        public static ref readonly LaundrySimulationResults InitialValue => ref TheInitialVal;

        public TimeSpan FinalElapsedTime => EndingTimeStamp.HasValue && StartingTimeStamp.HasValue
            ? EndingTimeStamp.Value - StartingTimeStamp.Value
            : TimeSpan.Zero;

        public TimeSpan TimeSinceStarted => StartingTimeStamp.HasValue
            ? StartingTimeStamp.Value - TimeStampSource.Now
            : TimeSpan.Zero;

        public bool NotStartedYet => StartingTimeStamp == null;
        public bool TerminatedBeforeFinish => LaundryItemsToGo > 0 && StartingTimeStamp != null && EndingTimeStamp != null;
        public bool InProgress => !Finished && !TerminatedBeforeFinish;
        public bool Finished => LaundryItemsToGo < 1 && StartingTimeStamp != null && EndingTimeStamp != null;
        public DateTime? StartingTimeStamp { get; }
        public DateTime? EndingTimeStamp { get; }
        public int LaundryItemsToGo { get; }
        
        [Pure]
        public LaundrySimulationResults Start(int startWithItems)
        {
            if (StartingTimeStamp != null || EndingTimeStamp != null || startWithItems < 1) throw new InvalidOperationException("Simulation already began.");
            return new LaundrySimulationResults(TimeStampSource.Now, null, startWithItems);
        }
        [Pure]
        public LaundrySimulationResults AnotherItemCleaned()
        {
            DateTime potentialClosingTs = TimeStampSource.Now;
            if (StartingTimeStamp == null || EndingTimeStamp != null || LaundryItemsToGo < 1) throw new InvalidOperationException($"Cannot call {nameof(AnotherItemCleaned)} in state {ToString()}.");
            return new LaundrySimulationResults(StartingTimeStamp.Value, LaundryItemsToGo -1 < 1 ? (DateTime?) potentialClosingTs : null, LaundryItemsToGo-1);
        }

        [Pure]
        public LaundrySimulationResults TerminateEarly()
        {
            DateTime ts = TimeStampSource.Now;
            if (StartingTimeStamp == null || EndingTimeStamp != null) throw new InvalidOperationException($"Cannot call {nameof(TerminateEarly)} in state {ToString()}.");
            return new LaundrySimulationResults(StartingTimeStamp.Value, ts, LaundryItemsToGo);
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
            lhs.StartingTimeStamp == rhs.StartingTimeStamp && lhs.EndingTimeStamp == rhs.EndingTimeStamp &&
            lhs.LaundryItemsToGo == rhs.LaundryItemsToGo;
        public static bool operator !=(LaundrySimulationResults lhs, LaundrySimulationResults rhs) => !(lhs == rhs);
        public bool Equals(LaundrySimulationResults other) => other == this;
        public override bool Equals(object other) => (other as LaundrySimulationResults? == this);
        public int CompareTo(LaundrySimulationResults other) => Compare(in this, in other);

        public override int GetHashCode()
        {
            int hash = StartingTimeStamp.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ EndingTimeStamp.GetHashCode();
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

        private LaundrySimulationResults(DateTime? startTs, DateTime? endTs, int itemsToGo)
        {
            StartingTimeStamp = startTs;
            EndingTimeStamp = endTs;
            LaundryItemsToGo = itemsToGo;
        }

        private static int Compare(in LaundrySimulationResults lhs, in LaundrySimulationResults rhs)
        {
            int ret;
            int startRes = Comp(lhs.StartingTimeStamp, rhs.StartingTimeStamp);
            
            if (startRes == 0)
            {
                int endRes = Comp(lhs.EndingTimeStamp, rhs.EndingTimeStamp);
                ret = endRes != 0 ? endRes : lhs.LaundryItemsToGo.CompareTo(rhs.LaundryItemsToGo);
            }
            else
            {
                ret = startRes;
            }

            return ret;

            int  Comp(DateTime? l, DateTime? r)
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