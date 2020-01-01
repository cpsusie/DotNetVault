using System;
using System.Collections.Generic;
using DotNetVault.UtilitySources;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault
{
    internal readonly struct IndividualFailureTriplet : IEquatable<IndividualFailureTriplet>, IComparable<IndividualFailureTriplet>
    {
        public readonly int IndexOfTypeParameter;

        public readonly ITypeParameterSymbol TypeParameterWithVsTpAttrib;
        
        public readonly ITypeSymbol OffendingNonVaultSafeTypeArgument;
        
        public IndividualFailureTriplet(int index, [NotNull] ITypeParameterSymbol tps, [NotNull] ITypeSymbol offendingNotVaultSafeType)
        {
            IndexOfTypeParameter = index > -1
                ? index
                : throw new ArgumentOutOfRangeException(nameof(index), index, @"Negative indices invalid");
            OffendingNonVaultSafeTypeArgument = offendingNotVaultSafeType ?? throw new ArgumentNullException(nameof(offendingNotVaultSafeType));
            TypeParameterWithVsTpAttrib = tps ?? throw new ArgumentNullException(nameof(tps));
        }

        public override string ToString() =>
            $"Param Idx: [{IndexOfTypeParameter}]; " +
            $"Type Param Name: [{TypeParameterWithVsTpAttrib?.Name ?? "NULL"}]; " +
            $"Bad Type Arg: [{OffendingNonVaultSafeTypeArgument?.Name}].";

        public static bool operator >(in IndividualFailureTriplet lhs, in IndividualFailureTriplet rhs) =>
            Compare(lhs, rhs) > 0;
        public static bool operator <(in IndividualFailureTriplet lhs, in IndividualFailureTriplet rhs) =>
            Compare(lhs, rhs) < 0;

        public static bool operator >=(in IndividualFailureTriplet lhs, in IndividualFailureTriplet rhs) =>
            !(lhs < rhs);
        public static bool operator <=(in IndividualFailureTriplet lhs, in IndividualFailureTriplet rhs) =>
            !(lhs > rhs);
        public static bool operator ==(in IndividualFailureTriplet lhs, in IndividualFailureTriplet rhs)
        {
            if (lhs.IndexOfTypeParameter != rhs.IndexOfTypeParameter) return false;
            if (!EqualityComparer<ISymbol>.Default.Equals(lhs.TypeParameterWithVsTpAttrib,
                rhs.TypeParameterWithVsTpAttrib)) return false;
            return EqualityComparer<ISymbol>.Default.Equals(lhs.OffendingNonVaultSafeTypeArgument,
                rhs.OffendingNonVaultSafeTypeArgument);
        }

        public static bool operator !=(in IndividualFailureTriplet lhs, in IndividualFailureTriplet rhs) => !(lhs == rhs);
        public int CompareTo(IndividualFailureTriplet other) => Compare(this, other);
        int IComparable<IndividualFailureTriplet>.CompareTo(IndividualFailureTriplet other) => Compare(this, other);
        public override int GetHashCode()
        {
            int hash = IndexOfTypeParameter;
            unchecked
            {
                hash = (hash * 397) ^ (TypeParameterWithVsTpAttrib?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (OffendingNonVaultSafeTypeArgument?.GetHashCode() ?? 0);
            }
            return hash;
        }

        public override bool Equals(object other) => (other as IndividualFailureTriplet?) == this;

        bool IEquatable<IndividualFailureTriplet>.Equals(IndividualFailureTriplet other) => other == this;
        public bool Equals(in IndividualFailureTriplet other) => this == other;
        private static int Compare(in IndividualFailureTriplet lhs, in IndividualFailureTriplet rhs)
        {
            int ret;
            int idxCompare = lhs.IndexOfTypeParameter.CompareTo(rhs.IndexOfTypeParameter);
            if (idxCompare == 0)
            {
                int offendingCompare = TypeNameComparerUtilitySource.TypeNameComparer.Compare(
                    lhs.OffendingNonVaultSafeTypeArgument?.Name, rhs.OffendingNonVaultSafeTypeArgument?.Name);
                ret = offendingCompare == 0
                    ? TypeNameComparerUtilitySource.TypeNameComparer.Compare(lhs.TypeParameterWithVsTpAttrib?.Name,
                        rhs.TypeParameterWithVsTpAttrib?.Name)
                    : offendingCompare;
            }
            else
            {
                ret = idxCompare;
            }
            return ret;
        }

        
    }
}