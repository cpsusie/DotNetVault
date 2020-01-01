using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault
{
    internal readonly struct IndividualAnalysisResult : IEquatable<IndividualAnalysisResult>
    {
        public ITypeSymbol ActualTypeWithViolation { get; }

        public ImmutableArray<IndividualFailureTriplet> FailureTriplets { get; }

        public IndividualAnalysisResult([NotNull] ITypeSymbol ts,
            ImmutableArray<IndividualFailureTriplet> failureTriplets)
        {
            ActualTypeWithViolation = ts ?? throw new ArgumentNullException(nameof(ts));
            FailureTriplets = failureTriplets;
        }

        public static bool operator ==(in IndividualAnalysisResult lhs, in IndividualAnalysisResult rhs)
        {
            if (!
                EqualityComparer<ISymbol>.Default.Equals(lhs.ActualTypeWithViolation, rhs.ActualTypeWithViolation)) return false;
            if (lhs.FailureTriplets.Length != rhs.FailureTriplets.Length) return false;

            for (int i = 0; i < lhs.FailureTriplets.Length; ++i)
            {
                if (lhs.FailureTriplets[i] != rhs.FailureTriplets[i])
                    return false;
            }
            return true;

        }

        public override string ToString() =>
            $"Type with violation: [{ActualTypeWithViolation?.Name ?? "NULL"}], # of violations: {FailureTriplets.Length}";
        

        public static bool operator !=(in IndividualAnalysisResult lhs, in IndividualAnalysisResult rhs) => !(lhs == rhs);
        public bool Equals(in IndividualAnalysisResult other) => this == other;
        bool IEquatable<IndividualAnalysisResult>.Equals(IndividualAnalysisResult other) => other == this;
        public override bool Equals(object obj) => obj is IndividualAnalysisResult ar && ar == this;
        

        public override int GetHashCode()
        {
            int hash = ActualTypeWithViolation?.GetHashCode() ?? 0;
            unchecked
            {
                return (hash * 397) ^ FailureTriplets.Length;
            }
        }
    }
}