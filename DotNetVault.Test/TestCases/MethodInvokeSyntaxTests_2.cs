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
                    TheRandomGen.SetNewValue(new Random((int) DateTime.Now.Ticks));
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
            var noDx = MethodGenerators.GeneratePair(() => new StringBuilder(), Guid.NewGuid, () => 12.9m,
                () => new AnimalName("Spencer"));
            var dx = MethodGenerators.GeneratePair(() => new AnimalName("Spencer"), () => 12.9m, Guid.NewGuid,
                () => GenerateStringBuilder(testNoDiag2, testNoDiag1));


            Console.WriteLine();
            Console.WriteLine("BEGIN TEST: {0}", methodName);

            Console.WriteLine("testNoDiag1: {0:O}", testNoDiag1);
            Console.WriteLine("testNoDiag2: {0}", testNoDiag2);
            Console.WriteLine("noDx: {0}", noDx);
            Console.WriteLine("dx: {0}", dx);

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

        public static (TNotVaultSafe NotFaultSafeObject, TVaultSafe VaultSafeObject, TAlsoVaultSafe VaultSafeObject2, TVaultSafeYetAgain VaultSafeObject3) GeneratePair<TNotVaultSafe,
            [VaultSafeTypeParam] TVaultSafe, [VaultSafeTypeParam] TAlsoVaultSafe, [VaultSafeTypeParam] TVaultSafeYetAgain>(Func<TNotVaultSafe> nvsGen, Func<TVaultSafe> vsGen, Func<TAlsoVaultSafe> alsoVsGen, Func<TVaultSafeYetAgain> vsyaGen)
        {
            TNotVaultSafe nvs = nvsGen();
            TVaultSafe vs = CreateVaultSafeObject(vsGen);
            TAlsoVaultSafe vsAlso = CreateVaultSafeObject(alsoVsGen);
            TVaultSafeYetAgain vsYa = vsyaGen();
            return (nvs, vs, vsAlso, vsYa);
        }
    }
}


