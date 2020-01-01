using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetVault.UtilitySources
{
    internal static class UsingMandatoryAttributeFinderSource
    {
        public static IUsingMandatoryAttributeFinder CreateUsingMandatoryAttributeFinder() => Factory();

        public static bool SupplyAlternateFactory([NotNull] Func<IUsingMandatoryAttributeFinder> alternate)
        {
            if (alternate == null) throw new ArgumentNullException(nameof(alternate));

            var old = Interlocked.CompareExchange(ref _factory, alternate, null);
            return old == null;
        }

        internal static UsingMandatoryAttributeFinder GetDefaultAttributeFinder() => new UsingMandatoryAttributeFinder();

        internal readonly struct UsingMandatoryAttributeFinder : IUsingMandatoryAttributeFinder
        {
            public bool HasUsingMandatoryReturnTypeSyntax(InvocationExpressionSyntax syntax, SemanticModel model)
            {
                if (syntax == null) throw new ArgumentNullException(nameof(syntax));
                if (model == null) throw new ArgumentNullException(nameof(model));

                
                bool ret;
                var symbolInfo = model.GetSymbolInfo(syntax);
                IMethodSymbol methSym = symbolInfo.Symbol as IMethodSymbol;
                if (methSym == null && symbolInfo.CandidateReason == CandidateReason.OverloadResolutionFailure &&
                    symbolInfo.CandidateSymbols.Any())
                {
                    methSym = symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                }


                if (methSym != null) 
                {
                    INamedTypeSymbol usingMandatoryAttributeSymbol =
                        model.Compilation.GetTypeByMetadataName(typeof(UsingMandatoryAttribute).FullName);
                    if (usingMandatoryAttributeSymbol != null)
                    {
                        var returnTypeAttributes = methSym.GetReturnTypeAttributes();
                        var candidateAttributes = from attribData in returnTypeAttributes
                            where attribData != null
                            let attribDataClass = attribData.AttributeClass
                            where attribDataClass.Equals(usingMandatoryAttributeSymbol, SymbolEqualityComparer.Default) || (
                                  attribDataClass is IErrorTypeSymbol ets &&
                                  ets.CandidateReason == CandidateReason.NotAnAttributeType &&
                                  ets.CandidateSymbols.Any(sym => sym.Name.StartsWith(UsingMandatoryAttribute.ShortenedName)))
                            select attribDataClass;
                        ret = candidateAttributes.Any();
                    }
                    else
                    {
                        ret = false;
                    }                    

                }
                else
                {
                    ret = false;
                }

                return ret;
            }
        }

        private static Func<IUsingMandatoryAttributeFinder> Factory
        {
            get
            {
                Func<IUsingMandatoryAttributeFinder> factory = _factory;
                if (factory == null)
                {
                    Func<IUsingMandatoryAttributeFinder> newFactory = () => GetDefaultAttributeFinder();
                    Interlocked.CompareExchange(ref _factory, newFactory, null);
                    factory = _factory;
                    Debug.Assert(factory != null);
                }
                return factory;
            }
        }

        private static volatile Func<IUsingMandatoryAttributeFinder> _factory;
    }
}
