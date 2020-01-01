using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    public sealed class Holder<[VaultSafeTypeParam] T>
    {
        public T Value { get; }

        public Holder(T addMe) => Value = addMe;
    }

    public static class TestWhiteListedVaultSafety
    {
        public static void TestMe()
        {
            Holder<System.Uri> hb = new Holder<System.Uri>(new System.Uri("http://www.google.com"));
            Console.WriteLine(hb.Value);
        }
    }
}