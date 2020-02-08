using System;
using System.Text;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    struct Bug64DemoShouldNotCompile
    {
        public DateTime TimeStamp { get; set; }

        public StringBuilder StatusText { get; }

        public Bug64DemoShouldNotCompile(DateTime ts, string text)
        {
            TimeStamp = ts;
            StatusText = new StringBuilder(text);
        }
    }
}
