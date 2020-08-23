using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetVault.Interfaces
{
    internal interface IUsingStatementSyntaxAnalyzer<in TInvocationSyntax> : IUsingStatementSyntaxAnalyzer
    {
        bool IsPartOfUsingConstruct([NotNull] TInvocationSyntax syntax);
        (bool PartOfInlineDeclUsing, VariableDeclarationSyntax ParentNode) IsPartOfInlineDeclUsingConstruct([NotNull] TInvocationSyntax syntax);
    }

    internal interface IUsingStatementSyntaxAnalyzer
    {
        Type ExpectedTypeOfSyntaxObject { get; }
        bool IsPartOfUsingConstruct([NotNull] object syntax);
        bool IsPArtOfInlineDeclUsingConstruct([NotNull] object syntax);
    }
}
