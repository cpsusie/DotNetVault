using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault.Interfaces
{
    internal interface IVaultSafeTypeAnalyzer
    {
        Task<(bool Result, Exception Error)> IsTypeVaultSafeAsync([NotNull] INamedTypeSymbol nts,
            [NotNull] Compilation comp, CancellationToken token);

        bool IsTypeVaultSafe([NotNull] INamedTypeSymbol nts, [NotNull] Compilation comp);
        bool IsTypeVaultSafe([NotNull] INamedTypeSymbol nts, [NotNull] Compilation comp, CancellationToken token);

        bool AnalyzeTypeParameterSymbolForVaultSafety([NotNull] ITypeParameterSymbol tps,
            [NotNull] Compilation comp);
    }
}
