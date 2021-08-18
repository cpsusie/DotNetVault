using DotNetVault.Attributes;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace DotNetVault.ExtensionMethods
{
    internal static class CompilationExtensions
    {
        [CanBeNull]
        public static INamedTypeSymbol FindReportWhiteListLocationsAttribute(this Compilation c) =>
            c?.GetTypeByMetadataName(typeof(ReportWhiteListLocationsAttribute).FullName!);
        [CanBeNull]
        public static INamedTypeSymbol FindVaultSafeAttribute(this Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(VaultSafeAttribute).FullName!);
        [CanBeNull]
        public static INamedTypeSymbol FindVaultSafeTypeParamAttribute(this Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(VaultSafeTypeParamAttribute).FullName);
        [CanBeNull]
        public static INamedTypeSymbol FindNoNonVsCaptureAttribute(this Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(NoNonVsCaptureAttribute).FullName);
        [CanBeNull]
        public static INamedTypeSymbol FindUsingMandatoryAttribute(this Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(UsingMandatoryAttribute).FullName);
        [CanBeNull]
        public static INamedTypeSymbol FindNotVsProtectableAttributeSymbol(this Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(NotVsProtectableAttribute).FullName);
        [CanBeNull]
        public static INamedTypeSymbol FindNoDirectInvokeAttribute(this Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(NoDirectInvokeAttribute).FullName);
        [CanBeNull]
        public static INamedTypeSymbol FindNoCopyAttribute(this Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(NoCopyAttribute).FullName);

        [CanBeNull]
        public static INamedTypeSymbol FindRefStructAttribute(this Compilation compilation)
        {
            string fullName = typeof(RefStructAttribute).FullName;
            return !string.IsNullOrWhiteSpace(fullName) ? compilation?.GetTypeByMetadataName(fullName) : null;
        }
    }
}
