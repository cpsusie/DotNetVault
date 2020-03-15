using System;
using System.Text;
using DotNetVault.Vaults;

namespace DotNetVault.CodeExamples
{
    internal static class MutableResourceVaultCreationExamples
    {
        internal static void CreateMutableResourceVaultTheCorrectWay()
        {
            var sbVault = MutableResourceVault<StringBuilder>.CreateAtomicMutableResourceVault(() => 
                new StringBuilder(), TimeSpan.FromMilliseconds(250));
            Console.WriteLine(@$"We just created a [{sbVault}] in the correct way -- " +
                              @"only the mutable resource vault will ever see the StringBuilder it constructs.");
        }
        internal static void CreateMutableResourceVaultTheIncorrectWay()
        {
            var sbVault = MutableResourceVault<StringBuilder>.CreateAtomicMutableResourceVault(() => BadPreExistingStringBuilder,
                TimeSpan.FromMilliseconds(250));
            Console.WriteLine(
                $@"We just created an [{sbVault}] in a very, very bad way.  "+
                @"The vault now protects a resource that pre-existed the vault."+  
                @$"  A change to {nameof(BadPreExistingStringBuilder)} is not thread-safe " +
                @"and will propagate to the vault's protected resource!");
        }

        private static readonly StringBuilder BadPreExistingStringBuilder = new StringBuilder();
    }

    internal static class SlightlyMoreSubtleExample
    {
        internal static void CreateMoreComplicatedMutableResourceTheCorrectWay()
        {
            var sbVault = MutableResourceVault<PrettyBadMoreComplexExample>
                .CreateAtomicMutableResourceVault(() =>

                {
                    var sbOne = new StringBuilder();
                    var sbTwo = new StringBuilder();
                    return new PrettyBadMoreComplexExample(sbOne, sbTwo);
                }, TimeSpan.FromMilliseconds(250));

            Console.WriteLine(@$"I just created a more complicated {sbVault} in the correct way." +
                              @"  Neither the PrettyBadMoreComplexExample nor any of its subobjects are " +
                              @"accessible outside the mutable resource vault.");
        }

        internal static void CreateMoreComplicatedMutableResourceInASlightlySubtleIncorrectWay(
            StringBuilder shouldBeSecond)
        {
            var sbVault =
                MutableResourceVault<PrettyBadMoreComplexExample>.CreateAtomicMutableResourceVault(() =>
                    {
                        var sbOne = new StringBuilder();
                        //VERY BAD!  Any change to ShouldBeSecond (which is accessible outside the vault)
                        //is not thread-safe
                        //and will propagate to the value in the vault!
                        return new PrettyBadMoreComplexExample(sbOne, shouldBeSecond);
                    },
                    TimeSpan.FromMilliseconds(250));
            Console.WriteLine(@$"I just created a {sbVault} in a very unsafe but subtle way.  " +
                              @$"If anyone changes the object referred to by {nameof(shouldBeSecond)}," +
                              @"It will propagate in an unsafe way to the value protected by the vault.");
        }
    }

    internal sealed class PrettyBadMoreComplexExample
    {
        public StringBuilder FirstBuilder { get; set; }
        public StringBuilder SecondBuilder { get; set; }

        internal PrettyBadMoreComplexExample(StringBuilder first, 
            StringBuilder second)
        {
            FirstBuilder = first;
            SecondBuilder = second;
        }
        internal PrettyBadMoreComplexExample()
        {
            FirstBuilder = new StringBuilder();
            SecondBuilder = new StringBuilder();
        }
    }
}
