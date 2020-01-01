using DotNetVault.Attributes;
//  /ReSharper disable All

namespace DotNetVault.Test.TestCases
{
    [VaultSafe(1 + 1 == 3)]
    public class ShouldTriggerDiagnosticNotSealedAndVsAttribute
    {
    }
}
