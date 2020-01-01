using System;
using System.Text;
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
            Holder<UriBuilder> hb = new Holder<UriBuilder>(new UriBuilder("http://www.google.com"));
            Holder<StringBuilder> sb = new Holder<StringBuilder>(new StringBuilder("Hi mom!"));
            Console.WriteLine(hb.Value);
            Console.WriteLine(sb.Value.ToString());
        }
    }
}