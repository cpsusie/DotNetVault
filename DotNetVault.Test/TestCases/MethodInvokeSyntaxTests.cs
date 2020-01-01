using System;
using System.Diagnostics;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.Miscellaneous;

// ReSharper disable LocalizableElement

namespace DotNetVault.Test.TestCases
{
    class MethodInvokeSyntaxTests
    {
        public static Random RGen
        {
            get
            {
                if (!TheRandomGen.IsSet)
                {
                    TheRandomGen.SetNewValue(new Random((int)DateTime.Now.Ticks));
                }
                Debug.Assert(TheRandomGen.IsSet);
                return TheRandomGen.Value;
            }
        }

        public static void TestUsingMethodInvocationSyntax()
        {
            const string methodName = nameof(TestUsingMethodInvocationSyntax);
            var testNoDiag1 = MethodGenerators.CreateVaultSafeObject(GenerateTimeStamp);
            var testNoDiag2 = MethodGenerators.CreateVaultSafeObject(() => GenerateName("Spencer"));

            var shouldGetDiag3 = MethodGenerators.CreateVaultSafeObject(() => GenerateStringBuilder(testNoDiag2, testNoDiag1));
            var noDx = MethodGenerators.GeneratePair(() => GenerateStringBuilder(testNoDiag2, testNoDiag1),
                GenerateTimeStamp);


            Console.WriteLine();
            Console.WriteLine("BEGIN TEST: {0}", methodName);

            Console.WriteLine("testNoDiag1: {0:O}", testNoDiag1);
            Console.WriteLine("testNoDiag2: {0}", testNoDiag2);
            Console.WriteLine("shouldGetDiag3: {0}", shouldGetDiag3);
            Console.WriteLine("noDx: {0}", noDx);

            Console.WriteLine("DONE TEST.");
            Console.WriteLine();

            StringBuilder GenerateStringBuilder(AnimalName name, DateTime ts)
            {
                var sb = new StringBuilder();
                sb.Append($"Animal id: {name.UniqueIdentifier}, Text: {name.Name}, TimeStamp: {ts:O}.");
                return sb;
            }

            DateTime GenerateTimeStamp()
            {
                var initial = DateTime.Now;
                TimeSpan ts = TimeSpan.FromDays(Random(2.1, 11.9));
                return initial + ts;
            }

            AnimalName GenerateName(string s) => new AnimalName(s ?? throw new ArgumentNullException(nameof(s)));

            double Random(double min, double max) => RGen.NextDouble() * (max - min) + min;
        }

        private static readonly ResourceManager<Random> TheRandomGen = new ResourceManager<Random>();

    }

    public static class MethodGenerators
    {
        public static TVaultSafe CreateVaultSafeObject<[VaultSafeTypeParam] TVaultSafe>(Func<TVaultSafe> vs)
        {
            return vs();
        }

        public static (TNotVaultSafe NotFaultSafeObject, TVaultSafe VaultSafeObject) GeneratePair<TNotVaultSafe,
            [VaultSafeTypeParam] TVaultSafe>(Func<TNotVaultSafe> nvsGen, Func<TVaultSafe> vsGen)
        {
            TNotVaultSafe nvs = nvsGen();
            TVaultSafe vs = CreateVaultSafeObject(vsGen);
            return (nvs, vs);
        }
    }
}
