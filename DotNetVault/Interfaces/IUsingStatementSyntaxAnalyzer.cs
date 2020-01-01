using System;
using JetBrains.Annotations;

namespace DotNetVault.Interfaces
{
    internal interface IUsingStatementSyntaxAnalyzer<in TInvocationSyntax> : IUsingStatementSyntaxAnalyzer
    {
        bool IsPartOfUsingConstruct([NotNull] TInvocationSyntax syntax);
    }

    internal interface IUsingStatementSyntaxAnalyzer
    {
        Type ExpectedTypeOfSyntaxObject { get; }
        bool IsPartOfUsingConstruct([NotNull] object syntax);
    }
}
