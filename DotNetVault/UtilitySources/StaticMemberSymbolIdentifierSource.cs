using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DefaultStaticSymbolIdentifier = DotNetVault.UtilitySources.StaticMemberSymbolIdentifierSource.StaticMemberSymbolIdentifier;
namespace DotNetVault.UtilitySources
{
    using DefaultStaticSymbolIdentifierFactory = Func<DefaultStaticSymbolIdentifier>;
    using StaticSymbolIdentifierFactory = Func<IStaticMemberSymbolIdentifier>;
    internal static class StaticMemberSymbolIdentifierSource
    {
        public static StaticSymbolIdentifierFactory FactoryInstance => TheFactory;

        public static bool SupplyAlternateFactory([NotNull] StaticSymbolIdentifierFactory alternate) =>
            TheFactory.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        internal static DefaultStaticSymbolIdentifierFactory DefaultFactory => TheDefaultFactory;

        internal readonly struct StaticMemberSymbolIdentifier : IStaticMemberSymbolIdentifier
        {
            public static IStaticMemberSymbolIdentifier CreateStaticMemberSymbolIdentifier() =>
                CreateStaticMemberSymbolIdentifierInstance();

            internal static StaticMemberSymbolIdentifier CreateStaticMemberSymbolIdentifierInstance() => new StaticMemberSymbolIdentifier();

            public ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols(SemanticModel model,
                SyntaxList<StatementSyntax> statements) =>
                FindPropertyOrFieldSymbols(model, statements, CancellationToken.None);
            public ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols(SemanticModel model,
                ExpressionStatementSyntax exprStatement) =>
                FindPropertyOrFieldSymbols(model, exprStatement, CancellationToken.None);
            public ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols(SemanticModel model,
                ExpressionSyntax expression) => FindPropertyOrFieldSymbols(model, expression, CancellationToken.None);
            

            public ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols(SemanticModel model,  ExpressionStatementSyntax exprStatement, CancellationToken token)
            {
                if (model == null) throw new ArgumentNullException(nameof(model));
                if (exprStatement == null) throw new ArgumentNullException(nameof(exprStatement));

                ImmutableHashSet<FindSymbolResult> symbolResults = ImmutableHashSet<FindSymbolResult>.Empty;
                ImmutableHashSet<FindSymbolResult>.Builder builder = symbolResults.ToBuilder();

                token.ThrowIfCancellationRequested();
                EnumerateNodes(ref builder, model, exprStatement.DescendantNodesAndSelf(), token);
                return builder.ToImmutable();

            }

            public ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols(SemanticModel model,ExpressionSyntax expression, CancellationToken token)
            {
                if (model == null) throw new ArgumentNullException(nameof(model));
                if (expression == null) throw new ArgumentNullException(nameof(expression));

                ImmutableHashSet<FindSymbolResult> symbolResults = ImmutableHashSet<FindSymbolResult>.Empty;
                ImmutableHashSet<FindSymbolResult>.Builder builder = symbolResults.ToBuilder();
                
                token.ThrowIfCancellationRequested();
                EnumerateNodes(ref builder, model, expression.DescendantNodesAndSelf(), token);
                return builder.ToImmutable();
            }

            public ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols(SemanticModel model,  SyntaxList<StatementSyntax> statements, CancellationToken token)
            {
                if (model == null) throw new ArgumentNullException(nameof(model));
                ImmutableHashSet<FindSymbolResult> symbolResults = ImmutableHashSet<FindSymbolResult>.Empty;
                ImmutableHashSet<FindSymbolResult>.Builder builder = symbolResults.ToBuilder();

                foreach (var syntax in statements)
                {
                    token.ThrowIfCancellationRequested();
                    EnumerateNodes(ref builder, model, syntax.DescendantNodesAndSelf(), token);
                }
                return builder.ToImmutable();
            }

            private void EnumerateNodes([NotNull] ref ImmutableHashSet<FindSymbolResult>.Builder resultSet, SemanticModel model, [NotNull] IEnumerable<SyntaxNode> nodeSource,
                CancellationToken token)
            {
                foreach (var node in nodeSource)
                {
                    token.ThrowIfCancellationRequested();
                    switch (node.Kind())
                    {
                        case SyntaxKind.IdentifierName:
                        case SyntaxKind.GenericName:
                            var symbol = model.GetSymbolInfo(node).Symbol;
                            if (symbol?.IsStatic == true)
                            {
                                switch (symbol)
                                {
                                    case IFieldSymbol fieldSymbol:
                                        resultSet.Add(FindSymbolResult.CreateFindSymbolResult(fieldSymbol, node));
                                        break;
                                    case IPropertySymbol propertySymbol:
                                        resultSet.Add(FindSymbolResult.CreateFindSymbolResult(propertySymbol, node));
                                        break;
                                }
                            }
                            break;
                    }
                }
            }

          
        }

        static StaticMemberSymbolIdentifierSource()
        {
            TheFactory = new LocklessWriteOnce<StaticSymbolIdentifierFactory>(() =>
                DefaultStaticSymbolIdentifier.CreateStaticMemberSymbolIdentifier);
        }

        private static readonly LocklessWriteOnce<StaticSymbolIdentifierFactory> TheFactory;
        private static readonly DefaultStaticSymbolIdentifierFactory TheDefaultFactory =
            DefaultStaticSymbolIdentifier.CreateStaticMemberSymbolIdentifierInstance;
    }

  
}
