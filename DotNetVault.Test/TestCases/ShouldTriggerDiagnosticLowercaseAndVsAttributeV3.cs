using DotNetVault.Attributes;
//  /ReSharper disable All

namespace DotNetVault.Test.TestCases
{
    [VaultSafe(false)]
    public class ShouldTriggerDiagnosticNotSealedAndVsAttribute
    {
    }
}
