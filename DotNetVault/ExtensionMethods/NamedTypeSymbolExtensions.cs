using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault.ExtensionMethods
{
    internal static class TypeSymbolExtensions
    {
        public static ImmutableArray<IFieldSymbol> GetFieldSymbolMembersIncludingBaseTypesExclObject([NotNull] this ITypeSymbol nts)
        {
            IEnumerable<ITypeSymbol> selfAndBaseTypes = nts.SelfTypeAndAllBaseTypesExceptObject();
            return (from typeSymbol in selfAndBaseTypes
                let fieldSymbols = typeSymbol.GetMembers().OfType<IFieldSymbol>()
                from fs in fieldSymbols
                where fs != null
                select fs).ToImmutableArray();
        }

        public static IEnumerable<ITypeSymbol> SelfTypeAndAllBaseTypesExceptObject([NotNull] this ITypeSymbol nts)
        {
            if (nts == null) throw new ArgumentNullException(nameof(nts));
            yield return nts;
            foreach (var itm in nts.AllBaseTypesExceptObject())
            {
                yield return itm;
            }
        }

        public static IEnumerable<ITypeSymbol> AllBaseTypesExceptObject([NotNull] this ITypeSymbol nts)
        {
            if (nts == null) throw new ArgumentNullException(nameof(nts));
            INamedTypeSymbol currentBaseType = nts.BaseType;
            while (currentBaseType != null && currentBaseType.SpecialType != SpecialType.System_Object)
            {
                yield return currentBaseType;
                currentBaseType = currentBaseType.BaseType;
            }
        }
    }

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