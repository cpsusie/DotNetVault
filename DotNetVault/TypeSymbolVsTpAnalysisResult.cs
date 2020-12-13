using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault
{
    internal readonly struct TypeSymbolVsTpAnalysisResult : IEquatable<TypeSymbolVsTpAnalysisResult>
    {
        public bool Passes => NonConformingResults.Length < 1;

        public ITypeSymbol NamedOrConstructedType { get; }

        public ImmutableArray<IndividualAnalysisResult> NonConformingResults { get; }
        
        public TypeSymbolVsTpAnalysisResult([NotNull] ITypeSymbol nts,
            ImmutableArray<IndividualAnalysisResult> analysisResults)
        {
            NamedOrConstructedType = nts ?? throw new ArgumentNullException(nameof(nts));
            NonConformingResults = analysisResults;
        }

        public override string ToString() =>
            $"Analyzed type symbol: [{NamedOrConstructedType?.Name ?? "NULL"}]; Passes: {Passes}].";

        public string PrintDiagnosticInfo() => TypeSymbolVsTpAnalysisResultPrinterSource.Printer(this);

        public static bool operator ==(in TypeSymbolVsTpAnalysisResult lhs, in TypeSymbolVsTpAnalysisResult rhs)
        {
            if (!EqualityComparer<ISymbol>.Default.Equals(lhs.NamedOrConstructedType, rhs.NamedOrConstructedType))
                return false;
            if (lhs.NonConformingResults.Length != rhs.NonConformingResults.Length) return false;
            for (int i = 0; i < lhs.NonConformingResults.Length; ++i)
            {
                if (lhs.NonConformingResults[i] != rhs.NonConformingResults[i]) return false;
            }

            return true;
        }

        public static bool operator !=(in TypeSymbolVsTpAnalysisResult lhs, in TypeSymbolVsTpAnalysisResult rhs) => !(lhs == rhs);

        public override int GetHashCode()
        {
            int hash = NamedOrConstructedType != null ? SymbolEqualityComparer.Default.GetHashCode(NamedOrConstructedType) : 0;
            unchecked
            {
                return (hash * 397) ^ NonConformingResults.GetHashCode();
            }
        }

        public override bool Equals(object obj) => obj is TypeSymbolVsTpAnalysisResult ts && this == ts;

        bool IEquatable<TypeSymbolVsTpAnalysisResult>.Equals(TypeSymbolVsTpAnalysisResult other) => this == other;

        public bool Equals(in TypeSymbolVsTpAnalysisResult other) => this == other;

    }
}