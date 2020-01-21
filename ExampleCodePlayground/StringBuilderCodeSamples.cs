using System;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;

namespace ExampleCodePlayground
{
    internal static class StringBuilderCodeSamples
    {
        public static void DemonstrateQueries()
        {
            const string methodName = nameof(DemonstrateQueries);
            Console.WriteLine();
            Console.WriteLine($"Performing {methodName}...");
            MutableResourceVault<StringBuilder> vault = CreateExampleVault();
            using var lck = vault.SpinLock();
            string contents = lck.ExecuteQuery((in StringBuilder res) => res.ToString());
            Console.WriteLine($"Contents: {contents}.");
            int ancillaryValue = 7;
            int lengthPlusAncillaryValue =
                lck.ExecuteQuery((in StringBuilder sb, in int anc) => sb.Length + anc,
                    ancillaryValue);
            Console.WriteLine("Length of contents (content length: " +
                              $"[{lck.ExecuteQuery( (in StringBuilder sb) => sb.Length)}]) " +
                              $"plus [{nameof(ancillaryValue)}] of [{ancillaryValue}]: " +
                              $"{lengthPlusAncillaryValue.ToString()}");
            int idx = 1;
            char offSet = (char) 32;
            char valOfCharSpecifiedIndexMadeUppercase =
                lck.ExecuteQuery(
                    (in StringBuilder sb, in char offset) => 
                        (char) (sb[idx] - offSet), offSet);
            Console.WriteLine($"Char at idx [{idx.ToString()}] " +
                              $"(current val: [{contents[1]}]) made upper " +
                              $"case: [{valOfCharSpecifiedIndexMadeUppercase}].");
            Console.WriteLine();

            Console.WriteLine("Bug50Demo3 START");
            Bug50Demo3();
            Console.WriteLine("Bug50Demo3 END");
        }

        public static void DemonstrateActions()
        {
            const string methodName = nameof(DemonstrateActions);
            Console.WriteLine();
            Console.WriteLine($"Performing {methodName}...");
            MutableResourceVault<StringBuilder> vault = CreateExampleVault();
            using var lck = vault.SpinLock();
            lck.ExecuteAction((ref StringBuilder res) =>
            {
                for (int i = 0; i < res.Length; ++i)
                {
                    char current = res[i];
                    if (char.IsLetter(current))
                    {
                        res[i] = char.IsLower(current) ? char.ToUpper(current) : char.ToLower(current);
                    }
                }
            });
            string contents = lck.ExecuteQuery((in StringBuilder sb)
                => sb.ToString());
            Console.WriteLine("Reversed Upper/Lower res: " +
                              $"{contents}");
            //now let's make every char at idx divisible by three change to q
            int divisibleBy = 3;
            lck.ExecuteAction((ref StringBuilder res, in int d) =>
            {
                for (int i = 0; i < res.Length; ++i)
                {
                    if (i % d == 0)
                    {
                        res[i] = 'q';
                    }
                }
            }, divisibleBy);
            contents = lck.ExecuteQuery((in StringBuilder sb)
                => sb.ToString());
            Console.WriteLine($"Made chars at idx divisble by 3 q: " +
                              $"[{contents}]");
            Console.WriteLine();
        }

        public static void DemonstrateUseOfExtensionMethodsToSimplify()
        {
            const string methodName = nameof(DemonstrateUseOfExtensionMethodsToSimplify);
            Console.WriteLine();
            Console.WriteLine($"Performing {methodName}...");
            MutableResourceVault<StringBuilder> vault = CreateExampleVault();
            using var lck = vault.SpinLock();
            Console.WriteLine("Contents: {0}", lck.GetContents());
            Console.WriteLine("First char: {0}", lck.GetCharAt(0));
            //make second char uppercase E
            lck.SetCharAt(1, 'E');
            Console.WriteLine("Changed to uppercase 'E': {0}", lck.GetContents());
            Console.WriteLine();
        }

        [VaultSafe]
        public struct IndexAndVal
        {
            public readonly string NewValue;
            public readonly int Idx;

            public IndexAndVal(string newVal, int idx)
            {
                NewValue = newVal;
                Idx = idx;
            }
        }

        public static void DemonstrateMixedOperations()
        {
            const string methodName = nameof(DemonstrateMixedOperations);
            Console.WriteLine();
            Console.WriteLine($"Performing {methodName}...");
            MutableResourceVault<StringBuilder> vault = CreateExampleVault();
            using var lck = 
                vault.SpinLock();

            //Find the index of the first char equal to 'o' and insert
            //" it's magic oooh oooh! " after it.
            //return the index and the new contents.
            string insertMe = " it's magic oooh oooh! ";
            char queryLetter = 'o';
            IndexAndVal res = lck.ExecuteMixedOperation(
                (ref StringBuilder sb, in char ql) =>
            {
                int idx = -1;
                for (int i = 0; i < sb.Length; ++i)
                {
                    if (sb[i] == ql)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx != -1)
                {
                    sb.Insert(idx + 1, insertMe);
                }

                return new IndexAndVal(sb.ToString(), idx);
            }, queryLetter);

            Console.WriteLine($"New value: [{res.NewValue}], " +
                              $"Index: [{res.Idx}].");
            Console.WriteLine();
        }

       //// BUG 50 Fix -- both of these now generate compiler errors
       // public static void Bug50Demonstration()
       // {
       //     var vault = CreateExampleVault();
       //     LockedVaultMutableResource<MutableResourceVault<StringBuilder>, StringBuilder> lck;
       //     using (lck = vault.SpinLock())
       //     {
       //         lck.SetCharAt(0, 'Q');
       //     }
       //     //BUG 50-- unsafe access to protected resource after disposal.  
       //     //BUG 50 workaround -- Always declare the locked resource inline;
       //     Console.WriteLine(lck.GetContents());
       // }

       // public static void Bug50Demonstration2()
       // {
       //     var vault = CreateExampleVault();
       //     LockedVaultMutableResource<MutableResourceVault<StringBuilder>, StringBuilder> lck;
       //     using (lck = vault.SpinLock())
       //     {
       //         //BUG 50-- protected resource will not be disposed post-assignment.  
       //         //BUG 50 workaround -- always declare and assign inline and do not attempt assignment;
       //         lck = default;
       //         lck.SetCharAt(0, 'Q');
       //     }

       //     Console.WriteLine(lck.GetContents());
       // }
        public static void Bug50Demo3()
        {
            var vault = CreateExampleVault();
            using var lck = vault.SpinLock();
            Console.WriteLine(lck.GetContents());
            //Compiler error ---
            //lck = default;
            //Console.WriteLine(lck.GetContents());
        }

        private static MutableResourceVault<StringBuilder> CreateExampleVault() =>
            MutableResourceVault<StringBuilder>.CreateMutableResourceVault(() => new StringBuilder("Hello, world!"),
                TimeSpan.FromMilliseconds(250));
    }
    
    internal static class LockedSbResExtensions
    {

        public static char GetCharAt(
            this in LockedVaultMutableResource<MutableResourceVault<StringBuilder>, 
                StringBuilder> lck,  int idx) =>  
            lck.ExecuteQuery((in StringBuilder sb, in int i) => sb[i], idx);

        public static void SetCharAt(
            this in LockedVaultMutableResource<MutableResourceVault<StringBuilder>, 
                StringBuilder> lck, int idx, char newC) 
            => lck.ExecuteAction((ref StringBuilder sb, in int i) => sb[i] = newC, idx);

        public static string GetContents(
            this in LockedVaultMutableResource<MutableResourceVault<StringBuilder>, StringBuilder> lck) =>
            lck.ExecuteQuery((in StringBuilder sb) => sb.ToString());
    }
}
