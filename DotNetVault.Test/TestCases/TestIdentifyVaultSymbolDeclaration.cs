using System;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomVaults;
using DotNetVault.Vaults;

namespace DotNetVault.Test.TestCases
{
    [NotVsProtectable]
    public sealed class FreakiddyFrackity
    {
        public int Age { get; set; }
        
        public int FavoriteNumber { get; set; }
    }

    public abstract class FakeVault<T>
    {
        public abstract T Resource { get; }
    }

    public class AnotherVersion<T> : FakeVault<T>
    {
        public sealed override T Resource { get; }

        public AnotherVersion(T val) => Resource = val;
    }

    public class StringBuilderedOnUp : AnotherVersion<StringBuilder>
    {
        public StringBuilderedOnUp(StringBuilder val) : base(val)
        {
        }
    }

    public class GotMyFreakOn : AnotherVersion<FreakiddyFrackity>
    {
        public GotMyFreakOn(FreakiddyFrackity val) : base(val)
        {
        }
    }

    public static class TestIdentifyVaultSymbolDeclaration
    {
        public static StringBuilderedOnUp CreateVault()
        {
            return new StringBuilderedOnUp(new StringBuilder("Foobar"));
        }

        public static GotMyFreakOn CreateFreakyVault() =>
            new GotMyFreakOn(new FreakiddyFrackity {Age = 42, FavoriteNumber = 4});
    }
}
