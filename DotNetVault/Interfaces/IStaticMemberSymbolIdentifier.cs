using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetVault.Interfaces
{
    internal interface IStaticMemberSymbolIdentifier
    {
        ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols([NotNull] SemanticModel model,
            SyntaxList<StatementSyntax> statements, CancellationToken token);
        ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols([NotNull] SemanticModel model,
            [NotNull] ExpressionStatementSyntax exprStatement, CancellationToken token);
        ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols([NotNull] SemanticModel model,
            [NotNull] ExpressionSyntax expression, CancellationToken token);
        ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols([NotNull] SemanticModel model,
            SyntaxList<StatementSyntax> statements);
        ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols([NotNull] SemanticModel model,
            [NotNull] ExpressionStatementSyntax exprStatement);
        ImmutableHashSet<FindSymbolResult> FindPropertyOrFieldSymbols([NotNull] SemanticModel model,
            [NotNull] ExpressionSyntax expression);
    }

    internal enum FindSymbolResultType
    {
        InvalidDefault = 0,
        Property,
        Field
    }

    internal readonly struct FindSymbolResult : IEquatable<FindSymbolResult>
    {
        public static FindSymbolResult CreateFindSymbolResult([NotNull] ISymbol symbol,
            [NotNull] SyntaxNode syntax)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            if (syntax == null) throw new ArgumentNullException(nameof(syntax));
            if (!symbol.IsStatic) throw new ArgumentException(@"Symbol must be static.", nameof(symbol));

            FindSymbolResultType type;
            switch (symbol)
            {
                case IPropertySymbol _:
                    type = FindSymbolResultType.Property;
                    break;
                case IFieldSymbol _:
                    type = FindSymbolResultType.Field;
                    break;
                default:
                    type = FindSymbolResultType.InvalidDefault;
                    break;
            }

            if (type == FindSymbolResultType.InvalidDefault)
            {
                throw new ArgumentException(@"Symbol must be a field or property.", nameof(symbol));
            }

            return new FindSymbolResult(symbol, syntax, type);

        }

        public readonly FindSymbolResultType ResultType;
        public readonly ISymbol StaticSymbol;
        public readonly SyntaxNode ContainingSyntax;

        private FindSymbolResult([NotNull] ISymbol propSymbol, [NotNull] SyntaxNode syntax, FindSymbolResultType type)
        {
            StaticSymbol = propSymbol;
            ContainingSyntax = syntax;
            ResultType = type;
        }

        public static bool operator ==(in FindSymbolResult lhs, in FindSymbolResult rhs)
            => lhs.ResultType == rhs.ResultType &&
               EqualityComparer<ISymbol>.Default.Equals(lhs.StaticSymbol, rhs.StaticSymbol) &&
               EqualityComparer<SyntaxNode>.Default.Equals(lhs.ContainingSyntax, rhs.ContainingSyntax);

        public static bool operator !=(FindSymbolResult lhs, FindSymbolResult rhs) => !(lhs == rhs);

        public override int GetHashCode()
        {
            int hash = ResultType.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ EqualityComparer<ISymbol>.Default.GetHashCode(StaticSymbol);
                hash = (hash * 397) ^ EqualityComparer<SyntaxNode>.Default.GetHashCode(ContainingSyntax);
            }
            return hash;
        }

        public override bool Equals(object other) => (other as FindSymbolResult?) == this;

        public bool Equals(FindSymbolResult other) => other == this;
    }
}
