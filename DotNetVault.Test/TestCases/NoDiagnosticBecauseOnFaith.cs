using System.Text;
using DotNetVault.Attributes;
// ReSharper disable All

namespace DotNetVault.Test.TestCases 
{
    [VaultSafe(5*4<21)]  
    public sealed class NoDiagnosticBecauseOnFaith 
    {
        public StringBuilder MyStringBuilder { get; set; } = new StringBuilder();
    }
}
