using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Logging;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using EarlyReleaseAnalyzer = DotNetVault.UtilitySources.EarlyReleaseAnalyzerFactorySource.EarlyReleaseAnalyzerImpl;
namespace DotNetVault.UtilitySources
{
    using EarlyReleaseAnalyzerFactory = Func<EarlyReleaseAnalyzer>;
    internal static class EarlyReleaseAnalyzerFactorySource
    {
        public static EarlyReleaseAnalyzerFactory Factory => TheFactory.Value;

        internal struct EarlyReleaseAnalyzerImpl
        {
            [CanBeNull]
            public INamedTypeSymbol FindEarlyReleaseAttributeSymbol([NotNull] Compilation comp)
            {
                if (comp == null) throw new ArgumentNullException(nameof(comp));
                INamedTypeSymbol attrib =
                    comp.GetTypeByMetadataName(typeof(EarlyReleaseAttribute).FullName);
                return attrib;
            }

            [CanBeNull]
            public INamedTypeSymbol FindEarlyReleaseJustificationAttribute([NotNull] Compilation comp)
            {
                if (comp == null) throw new ArgumentNullException(nameof(comp));
                INamedTypeSymbol attrib =
                    comp.GetTypeByMetadataName(typeof(EarlyReleaseJustificationAttribute).FullName);
                return attrib;
            }

            public (EarlyReleaseReason? Reason, IMethodSymbol EnclosingMethodSymbol, Location InvocationLocation, Location EnclosingMethodLocation) GetEarlyReleaseJustification([NotNull] Compilation comp,
                [NotNull] InvocationExpressionSyntax ies,
                CancellationToken token)
            {
                if (comp == null) throw new ArgumentNullException(nameof(comp));
                if (ies == null) throw new ArgumentNullException(nameof(ies));
                EarlyReleaseReason? ret = null;
                IMethodSymbol enclosingMethodSym = null;
                Location invocationLocation = ies.GetLocation();
                Location enclosingMethodLocation = null;
                var semanticModel = comp.GetSemanticModel(ies.SyntaxTree, true);
                if (semanticModel != null)
                {
                    INamedTypeSymbol earlyReleaseJustificationAttrib = FindEarlyReleaseJustificationAttribute(comp);
                    var enclosingMethod = ies.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    if (enclosingMethod != null && earlyReleaseJustificationAttrib != null)
                    {
                        var symbolInfo = semanticModel.GetDeclaredSymbol(enclosingMethod, token);
                        if (symbolInfo is IMethodSymbol enclosingMethodSymbol)
                        {
                            enclosingMethodSym = enclosingMethodSymbol;
                            enclosingMethodLocation = enclosingMethodSym.Locations.FirstOrDefault();
                            var justRes = ExtractJustification(enclosingMethodSymbol,
                                earlyReleaseJustificationAttrib, comp);
                            LogWhetherHasJustificationAttribute(justRes.HasAttribute,
                                enclosingMethodSymbol.Name ?? "UNKNOWN");
                            ret = justRes.PropertyValue;
                        }

                    }
                }
                Debug.Assert(!ret.HasValue || ret.Value.IsDefined());
                Debug.Assert(!ret.HasValue || (enclosingMethodSym != null && invocationLocation != null &&
                                               enclosingMethodLocation != null));
                return  (ret, enclosingMethodSym, invocationLocation, enclosingMethodLocation);
            }

            [Conditional("DEBUG")]
            [SuppressMessage("ReSharper", "IdentifierTypo")]
            private static void LogWhetherHasJustificationAttribute(bool hasAttrib,
                [NotNull] string enclosingMethSymName)
            {
                Debug.Assert(!string.IsNullOrEmpty(enclosingMethSymName));
                string hasOrDoesntHave = hasAttrib ? "has" : "does not have";
                // ReSharper disable once RedundantAssignment
                string msg =
                    $"The enclosing method {enclosingMethSymName} {hasOrDoesntHave} the {typeof(EarlyReleaseJustificationAttribute).Name} attribute.";
                DebugLog.Log(msg);
            }

            public bool IsEarlyReleaseCall([NotNull] Compilation comp, [NotNull] InvocationExpressionSyntax ies,
                CancellationToken token)
            {
                if (comp == null) throw new ArgumentNullException(nameof(comp));
                if (ies == null) throw new ArgumentNullException(nameof(ies));

                bool ret = false;
                INamedTypeSymbol earlyReleaseAttrib = FindEarlyReleaseAttributeSymbol(comp);
                if (earlyReleaseAttrib != null)
                {
                    var semanticModel = comp.GetSemanticModel(ies.SyntaxTree, true);
                    if (semanticModel != null)
                    {
                        var msi = ModelExtensions.GetSymbolInfo(semanticModel, ies, token);
                        if (msi.Symbol != null)
                        {
                            var methodSymbol = msi.Symbol;
                            var attribDataCol = methodSymbol.GetAttributes();
                            var candidateAttributes = from attribData in attribDataCol
                                where attribData != null
                                let attribDataClass = SymbOrThrowIfCancReq(attribData.AttributeClass, token)
                                where SymbolEqualityComparer.Default.Equals(earlyReleaseAttrib, attribDataClass)   || (
                                          attribDataClass is IErrorTypeSymbol ets &&
                                          ets.CandidateReason == CandidateReason.NotAnAttributeType &&
                                          ets.CandidateSymbols.Any(sym =>
                                              sym.Name.StartsWith(EarlyReleaseAttribute.ShortenedName)))
                                select attribDataClass;
                            ret = candidateAttributes.Any();
                        }
                    }
                }
                return ret;
            }
        }

        private static INamedTypeSymbol SymbOrThrowIfCancReq(INamedTypeSymbol nts, CancellationToken tkn)
        {
            tkn.ThrowIfCancellationRequested();
            return nts;
        }

        private static bool IsNtsAnAttributeOfTypeAttributeSymbol(INamedTypeSymbol nts,
            INamedTypeSymbol attributeSymbol)
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
         
        private static (bool HasAttribute, EarlyReleaseReason? PropertyValue) ExtractJustification(IMethodSymbol querySymbol, INamedTypeSymbol justification,
                Compilation model) 
        {
            bool hasAttrib = false;
            if (querySymbol == null || justification == null) return (false, null);
            var queryRes = FindFirstMatchingAttribData(querySymbol, justification);
            if (queryRes == default) return (false, null);

            EarlyReleaseReason? ret = null;
            int? backer;
            var matchingData = queryRes.AttribData;
            var matchingAttrbClass = queryRes.MatchingAttribDataClass;
            bool isMatch = matchingAttrbClass?.Equals(justification, SymbolEqualityComparer.Default) == true;
            if (isMatch && matchingData != null)
            {
                hasAttrib = true;
                backer = matchingData.ConstructorArguments.Length == 1 &&
                      matchingData.ConstructorArguments[0].Value is int x ? (int?) x : null;
                ret = EarlyReleaseExtensionMethods.ConvertIntToEnumIfDefinedElseNull(backer);
            }
            else if (matchingData != null && matchingAttrbClass != null)
            {
                var syntaxTree = matchingData.ApplicationSyntaxReference.SyntaxTree;
                if (syntaxTree != null)
                {
                    var nodes = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(mds => mds.Identifier.Text == querySymbol.Name);
                    var attributeListSyntax = nodes?.DescendantNodes().OfType<AttributeListSyntax>();

                    var attributeSyntaxes = attributeListSyntax?.SelectMany(als => als.Attributes).ToList() ??
                                            new List<AttributeSyntax>();
                    var attribInQuestion = attributeSyntaxes.FirstOrDefault(attrsyn =>
                        attrsyn.DescendantNodes().OfType<IdentifierNameSyntax>().Any(ins =>
                            ins.Identifier.ValueText == nameof(EarlyReleaseJustificationAttribute) ||
                            ins.Identifier.ValueText == EarlyReleaseJustificationAttribute.ShortenedName));
                    hasAttrib = attribInQuestion != null;
                    bool hasArguments = attribInQuestion?.ArgumentList?.Arguments.Count == 1;
                    if (hasArguments)
                    {
                        var firstArgument = attribInQuestion.ArgumentList?.Arguments[0];
                        var firstArgumentExpression = firstArgument?.Expression;
                        if (firstArgumentExpression != null)
                        {
                            var semanticModel = model.GetSemanticModel(syntaxTree);
                            var constV = semanticModel.GetConstantValue(firstArgumentExpression);
                            backer = constV.HasValue && constV.Value is int x ? (int?) x : null;
                            ret = EarlyReleaseExtensionMethods.ConvertIntToEnumIfDefinedElseNull(backer); 
                        }
                    }
                }
            }
            

            return (hasAttrib, ret);

            static (AttributeData AttribData, INamedTypeSymbol MatchingAttribDataClass)
                FindFirstMatchingAttribData(
                    IMethodSymbol methSym, INamedTypeSymbol canonical) =>
                (from ad in methSym.GetAttributes()
                 let matches = IsNtsAnAttributeOfTypeAttributeSymbol(ad.AttributeClass, canonical)
                 where matches
                 select (ad, ad.AttributeClass)).FirstOrDefault();
        }

        private static readonly LocklessWriteOnce<EarlyReleaseAnalyzerFactory> TheFactory = new LocklessWriteOnce<EarlyReleaseAnalyzerFactory>((() => ()=> new EarlyReleaseAnalyzer()));
    }
}
