using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Logging;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using BvProtResAnalyzer = DotNetVault.UtilitySources.BvProtResAnalyzerFactorySource.BvProtResAnalyzerImpl;
namespace DotNetVault.UtilitySources
{
    using BvProtResAnalyzerFactory = Func<BvProtResAnalyzer>;
    using IBvRosRetAnalyzerFactory = Func<IBvProtResAnalyzer>;
    internal static class BvProtResAnalyzerFactorySource
    {
        internal static IBvRosRetAnalyzerFactory FactoryInstance => () => DefaultFactoryInstance();
        internal static BvProtResAnalyzerFactory DefaultFactoryInstance => TheFactoryInstance.Value;

        internal static bool TrySetNotDefaultFactory([NotNull] BvProtResAnalyzerFactory alternate) =>
            TheFactoryInstance.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        internal struct BvProtResAnalyzerImpl : IBvProtResAnalyzer
        {
            [NotNull]
            public INamedTypeSymbol FindBvProtResAttribute(Compilation comp) =>
                TryFindBvProtResAttribute(comp ?? throw new ArgumentNullException(nameof(comp))) ??
                throw new InvalidOperationException(
                    $"Unable to find {typeof(BasicVaultProtectedResourceAttribute).Name} in the supplied compilation.");

            [CanBeNull]
            public INamedTypeSymbol TryFindBvProtResAttribute(Compilation compilation)
                => (compilation ?? throw new ArgumentNullException(nameof(compilation))).GetTypeByMetadataName(
                    typeof(BasicVaultProtectedResourceAttribute).FullName);

            public bool QueryContainsIllegalRefExpression(Compilation comp, RefExpressionSyntax syntax,
                SemanticModel model, CancellationToken token)
            {
                if (comp == null) throw new ArgumentNullException(nameof(comp));
                if (syntax == null) throw new ArgumentNullException(nameof(syntax));
                if (model == null) throw new ArgumentNullException(nameof(model));

                INamedTypeSymbol bvProtResAttrib = FindBvProtResAttribute(comp);
                DebugLog.Log($"Analyzing for symbol {bvProtResAttrib.Name}.");
                token.ThrowIfCancellationRequested();

                var findPairs = ExtractMemberAccessExprAndMatchingPropSymbols(comp,
                    syntax, model, token);
                var propertiesWithAttribute = from item in findPairs
                    let propSymb = item.PropSymbol
                    from attribInfo in propSymb.GetAttributes()
                    let temp = TokenOrThrow(token)
                    where AreAttributesEqual(attribInfo.AttributeClass, bvProtResAttrib)
                    select propSymb;

                return propertiesWithAttribute.Any();

                static CancellationToken TokenOrThrow(CancellationToken tkn)
                {
                    tkn.ThrowIfCancellationRequested();
                    return tkn;
                }
            }

            private IEnumerable<(MemberAccessExpressionSyntax Syntax, IPropertySymbol PropSymbol)>
                ExtractMemberAccessExprAndMatchingPropSymbols([NotNull] Compilation comp,
                    [NotNull] RefExpressionSyntax syntax,
                    [NotNull] SemanticModel model, CancellationToken token)
            {
                IEnumerable<(MemberAccessExpressionSyntax AcExSyn, SimpleNameSyntax NameSyntax)> temp =
                    from item in syntax.ChildNodes().OfType<MemberAccessExpressionSyntax>()
                    where item.OperatorToken.IsKind(SyntaxKind.DotToken)
                    let maes = item
                    where maes.Name != null
                    select (maes, maes.Name);
                return from tupl in temp
                    let maes = tupl.AcExSyn
                    let res = model.GetSymbolInfo(tupl.NameSyntax, token)
                    where res.Symbol is IPropertySymbol 
                    select (maes, (IPropertySymbol) res.Symbol);
            }

            private static bool
                AreAttributesEqual(INamedTypeSymbol attribClass, [NotNull] INamedTypeSymbol canonicalAttrib) =>
                SymbolEqualityComparer.Default.Equals(attribClass, canonicalAttrib) ||
                attribClass.MetadataName == canonicalAttrib.MetadataName;

        }


        private static readonly LocklessWriteOnce<BvProtResAnalyzerFactory> TheFactoryInstance =
            new LocklessWriteOnce<BvProtResAnalyzerFactory>(() => () => new BvProtResAnalyzer());
    }

    internal interface IBvProtResAnalyzer
    {
        [NotNull] INamedTypeSymbol FindBvProtResAttribute([NotNull] Compilation comp);
        [CanBeNull] INamedTypeSymbol TryFindBvProtResAttribute([NotNull] Compilation compilation);
        bool QueryContainsIllegalRefExpression([NotNull] Compilation comp, [NotNull] RefExpressionSyntax syntax,
            [NotNull] SemanticModel model, CancellationToken token);

    }
}
