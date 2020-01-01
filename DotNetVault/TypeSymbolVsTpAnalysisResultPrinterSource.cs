using System;
using System.Linq;
using DotNetVault.Logging;
using JetBrains.Annotations;

namespace DotNetVault
{
    internal delegate string ResPrinter(in TypeSymbolVsTpAnalysisResult printMe);

    internal static class TypeSymbolVsTpAnalysisResultPrinterSource
    {
        public static ResPrinter Printer => ThePrinter;

        public static bool SupplyAlternatePrinter([NotNull] ResPrinter alternate) =>
            ThePrinter.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        private static string PrintIt(in TypeSymbolVsTpAnalysisResult printMe)
        {
            string ret;
            if (printMe.Passes)
            {
                ret = $"No problems found for {printMe.NamedOrConstructedType.Name ?? string.Empty}.";
            }
            else
            {
                var result = printMe.NonConformingResults.First(itm => itm.FailureTriplets.Any());
                string actualTypeName = result.ActualTypeWithViolation?.Name ?? string.Empty;
                string analyzedTypeName = printMe.NamedOrConstructedType.Name ?? string.Empty;
                var firstRes = result.FailureTriplets.First();

                string msgPart2 =
                    $"  type parameter [Name: {firstRes.TypeParameterWithVsTpAttrib?.Name ?? string.Empty}; " +
                    $"Param#: {firstRes.IndexOfTypeParameter}] MUST be vault-safe.  " +
                    $"The type argument [{firstRes.OffendingNonVaultSafeTypeArgument.Name ?? string.Empty}], " +
                    "however, is not vault-safe.";
                string msgPart1 = actualTypeName == analyzedTypeName
                    ? $"Type {actualTypeName}'s"
                    : $"{analyzedTypeName}'s ancestor {actualTypeName}'s'";
                ret= $"{msgPart1}{msgPart2}";
            }
            return ret;
        }

        private static readonly LocklessWriteOnce<ResPrinter> ThePrinter = new LocklessWriteOnce<ResPrinter>(() => PrintIt);
    }
}