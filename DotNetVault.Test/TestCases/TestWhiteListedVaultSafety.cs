using System;
//using System.Text;
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
            Holder<string> hb = new Holder<string>("Hi mom");
            //Holder<StringBuilder> hb = new Holder<StringBuilder>(new StringBuilder());
            Console.WriteLine(hb.Value);
        }
    }
}
