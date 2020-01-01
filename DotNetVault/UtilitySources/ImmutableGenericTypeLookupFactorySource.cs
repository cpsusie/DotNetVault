using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Enumerator = System.Collections.Immutable.ImmutableDictionary<string, Microsoft.CodeAnalysis.INamedTypeSymbol>.Enumerator;
namespace DotNetVault.UtilitySources
{
    using IImmutableGenericTypeLookup = IImmutableGenericTypeLookup<Enumerator>;

    internal delegate IImmutableGenericTypeLookup ImmutableGenericTypeLookupFactory(
        ImmutableArray<string> metaDataNamesOfUnboundGenericTypes, Compilation compilation);

    internal static class ImmutableGenericTypeLookupFactorySource
    {
        public static ImmutableGenericTypeLookupFactory CreateFactoryInstance() => TheFactory;

        public static bool SupplyAlternateFactory([NotNull] ImmutableGenericTypeLookupFactory alternate) =>
            TheFactory.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        #region Default Impl of IImutableGenericTypeLookup
        private sealed class ImmutableGenericTypeLookup : IImmutableGenericTypeLookup<Enumerator>
        {
            #region Factory Fucntion
            internal static IImmutableGenericTypeLookup CreateInstance([ItemNotNull]
                ImmutableArray<string> metadataNamesOfUnboundGenericTypes, [NotNull] Compilation compilation) =>
                    new ImmutableGenericTypeLookup(metadataNamesOfUnboundGenericTypes,
                        compilation ?? throw new ArgumentNullException(nameof(compilation)));
            #endregion

            #region Public Properties
            public int Count => _lookup.Count;
            public INamedTypeSymbol this[string metaDataName] => _lookup[metaDataName];
            public IEnumerable<string> Keys => _lookup.Keys;
            public IEnumerable<INamedTypeSymbol> Values => _lookup.Values;
            #endregion

            #region Private CTOR
            private ImmutableGenericTypeLookup(ImmutableArray<string> metadataNamesOfUnboundGenericTypes,
                    Compilation compilation)
            {
                ImmutableArray<(string MetaDataName, INamedTypeSymbol MatchingType)> resultArray =
                    FindMatchingTypeForEachMetadataName(metadataNamesOfUnboundGenericTypes, compilation).ToImmutableArray();

                var lookup = ImmutableDictionary<string, INamedTypeSymbol>.Empty;
                foreach (var item in resultArray)
                {
                    if (!lookup.ContainsKey(item.MetaDataName))
                    {
                        lookup = lookup.Add(item.MetaDataName, item.MatchingType);
                    }
                }
                _lookup = lookup;
                _compilation = compilation;
            }
            #endregion

            #region Public Methods
            public Enumerator GetEnumerator() => _lookup.GetEnumerator();
            public bool ContainsKey(string metaDataName) => _lookup.ContainsKey(metaDataName);
            public (bool IsDesignated, INamedTypeSymbol MatchingOpenTypeSymbol) FindMatch(ITypeSymbol typeSymbol)
            {
                if (typeSymbol == null) throw new ArgumentNullException(nameof(typeSymbol));

                if (typeSymbol is IErrorTypeSymbol ets)
                {
                    var resolution = ets.ResolveErrorTypeSymbol(_compilation);
                    typeSymbol = resolution ?? typeSymbol;
                }

                string metaDataNameOfUnboundTypeIfPossible;
                if (typeSymbol is INamedTypeSymbol nts)
                {
                    metaDataNameOfUnboundTypeIfPossible = nts.IsUnboundGenericType
                        ? nts.FullMetaDataName(true)
                        : nts.ConstructedFrom?.FullMetaDataName(true) ?? nts.FullMetaDataName(true) ?? string.Empty;
                }
                else if (typeSymbol is IArrayTypeSymbol _)
                {
                    metaDataNameOfUnboundTypeIfPossible = typeSymbol.FullMetaDataName(true) ?? string.Empty;
                }
                else if (typeSymbol is IDynamicTypeSymbol _)
                {
                    metaDataNameOfUnboundTypeIfPossible = string.Empty;
                }
                else
                {
                    metaDataNameOfUnboundTypeIfPossible = (typeSymbol.ContainingNamespace != null)
                        ? typeSymbol.FullMetaDataName(true) ?? string.Empty
                        : string.Empty;

                }

                bool isDesignated = _lookup.TryGetValue(metaDataNameOfUnboundTypeIfPossible,
                                        out INamedTypeSymbol matchingOpenTypeSymbol) &&
                                    matchingOpenTypeSymbol != null;
                return (isDesignated, matchingOpenTypeSymbol);
            }

            public (bool IsDesignated, INamedTypeSymbol MatchingOpenTypeSymbol) FindMatch(string typeMetaDataName)
            {
                if (typeMetaDataName == null) throw new ArgumentNullException(nameof(typeMetaDataName));

                bool isDesignated;
                bool foundRightAway = _lookup.TryGetValue(typeMetaDataName, out INamedTypeSymbol designatedSymbol) &&
                                      designatedSymbol != null;
                if (foundRightAway)
                {
                    isDesignated = true;
                }
                else
                {
                    INamedTypeSymbol nts = _compilation.GetTypeByMetadataName(typeMetaDataName);
                    if (nts != null)
                    {
                        var result = FindMatch(nts);
                        isDesignated = result.IsDesignated;
                        designatedSymbol = result.MatchingOpenTypeSymbol;
                    }
                    else
                    {
                        isDesignated = false;
                    }
                }

                return (isDesignated && designatedSymbol != null, designatedSymbol);
            }
            #endregion

            #region Explicitly implemented methods
            bool IReadOnlyDictionary<string, INamedTypeSymbol>.TryGetValue(string key, out INamedTypeSymbol value) =>
                    _lookup.TryGetValue(key, out value);

            IEnumerator<KeyValuePair<string, INamedTypeSymbol>> IEnumerable<KeyValuePair<string, INamedTypeSymbol>>.
                GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            #endregion

            #region Private Methods
            private IEnumerable<(string MetaDataName, INamedTypeSymbol MatchingType)>
                    FindMatchingTypeForEachMetadataName(ImmutableArray<string> metaDataNames, [NotNull] Compilation compilation)
            {
                foreach (var s in metaDataNames)
                {
                    INamedTypeSymbol symbol = compilation.GetTypeByMetadataName(s);
                    if (symbol != null) yield return (s, symbol);
                }
            }
            #endregion

            #region Private Fields

            private readonly Compilation _compilation;
            private readonly ImmutableDictionary<string, INamedTypeSymbol> _lookup;
            #endregion
        }
        #endregion

        #region PRIVATES (Factory Factory)
        private static readonly LocklessWriteOnce<ImmutableGenericTypeLookupFactory> TheFactory =
            new LocklessWriteOnce<ImmutableGenericTypeLookupFactory>(() => ImmutableGenericTypeLookup.CreateInstance); 
        #endregion
    }
}
