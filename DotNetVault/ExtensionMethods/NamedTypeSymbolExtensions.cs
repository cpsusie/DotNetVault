using System;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault.ExtensionMethods
{
    internal static class NamedTypeSymbolExtensions
    {
        public static bool DoesNamedTypeHaveAttribute(this INamedTypeSymbol querySymbol, [NotNull] INamedTypeSymbol canonicalSymbolToFind)
        {
            if (querySymbol == null) throw new ArgumentNullException(nameof(querySymbol));
            if (canonicalSymbolToFind == null) throw new ArgumentNullException(nameof(canonicalSymbolToFind));

            return querySymbol.GetAttributes().Any(attribData =>
                IsNtsAnAttributeOfTypeAttributeSymbol(attribData.AttributeClass, canonicalSymbolToFind));
        }

        public static bool IsNtsAnAttributeOfTypeAttributeSymbol(this INamedTypeSymbol nts, INamedTypeSymbol attributeSymbol)
        {
            if (ReferenceEquals(nts, attributeSymbol)) return true;
            if (ReferenceEquals(nts, null) || ReferenceEquals(attributeSymbol, null)) return false;
            if (nts.Equals(attributeSymbol, SymbolEqualityComparer.Default)) return true;

            if (nts is IErrorTypeSymbol ets && ets.CandidateReason == CandidateReason.NotAnAttributeType)
            {
                foreach (var item in ets.CandidateSymbols.OfType<INamedTypeSymbol>())
                {
                    if (item.Equals(attributeSymbol, SymbolEqualityComparer.Default)) return true;
                }
            }
            return false;
        }
    }
}