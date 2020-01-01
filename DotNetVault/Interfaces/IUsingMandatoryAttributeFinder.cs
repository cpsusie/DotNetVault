using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetVault.Interfaces
{
    internal interface IUsingMandatoryAttributeFinder
    {
        bool HasUsingMandatoryReturnTypeSyntax([NotNull] InvocationExpressionSyntax syntax,
            [NotNull] SemanticModel model);
    }
}
