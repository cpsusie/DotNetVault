using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault.Interfaces
{
    internal interface IImmutableGenericTypeLookup : IReadOnlyDictionary<string, INamedTypeSymbol>
    {
        /// <summary>
        /// Check to see whether a (potentially closed) named type symbol is one of the immutable generic types
        /// </summary>
        /// <param name="typeSymbol">the type symbol to check</param>
        /// <returns>A tuple whose IsDesignated property is true for a match, false for no match.  If a match, the MatchingOpenTypeSymbol
        /// will be the type symbol representing the unbound generic type that is a match.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeSymbol"/> was null</exception>
        (bool IsDesignated, INamedTypeSymbol MatchingOpenTypeSymbol) FindMatch([NotNull] ITypeSymbol typeSymbol);

        /// <summary>
        /// Check whether the metadataName refers to a (potentially closed) named type symbol that is one of the immutable generic types
        /// </summary>
        /// <param name="typeMetaDataName">The metadata name of type type to find</param>
        /// <returns>A tuple whose IsDesignated property is true for a match, false for no match.  If a match, the MatchingOpenTypeSymbol
        /// will be the type symbol representing the unbound generic type that is a match.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeMetaDataName"/> was null</exception>
        (bool IsDesignated, INamedTypeSymbol MatchingOpenTypeSymbol) FindMatch([NotNull] string typeMetaDataName);

        
    }

    internal interface IImmutableGenericTypeLookup<TKvpEnumerator> : IImmutableGenericTypeLookup where TKvpEnumerator : struct, IEnumerator<KeyValuePair<string, INamedTypeSymbol>>
    {
        new TKvpEnumerator GetEnumerator();
    }
}
