
using System;
using System.Text;
using DotNetVault.Attributes;

namespace ExampleCodePlayground
{
    public class NotVaultSafeClass
    {
        public DateTime TimeStamp { get; set; }

        public StringBuilder Sb { get; set; }

        public override string ToString() =>
            $"[{nameof(NotVaultSafeClass)}] -- Timestamp: [{TimeStamp:O}]; StringBuilder: [{Sb?.ToString() ?? "NULL"}].";
    }

    public static class NameColonSyntaxExamples
    {
        public static string PrintStuff<TNotVaultSafe1, [VaultSafeTypeParam] TVaultSafe1,
            [VaultSafeTypeParam] TVaultSafe2, TNotVaultSafe2>(TNotVaultSafe1 nvs1, TVaultSafe1 vs1, TVaultSafe2 vs2,
            TNotVaultSafe2 nvs2) =>
            $"nvs1: [{nvs1?.ToString() ?? "NULL"}]; vs1: [{vs1?.ToString() ?? "NULL"}]; vs2: [{vs2?.ToString() ?? "NULL"}], nvs2: [{nvs2?.ToString() ?? "NULL"}].";

        /// <summary>
        /// Task 26 -- no need to modify analyzer -- this demonstrates it works as-is.
        /// </summary>
        public static void Demonstrate()
        {
            DateTime firstVs = DateTime.Now;
            StringBuilder firstNotVs = new StringBuilder(nameof(firstNotVs));
            NotVaultSafeClass secondNotVs = new NotVaultSafeClass {Sb = firstNotVs, TimeStamp = firstVs};
            ulong secondVs = 0xb00b_cafe_face_dead;

            //This should work
            PrintStuff(firstNotVs, firstVs, secondVs, secondNotVs);

            //This shouldn't work
            //PrintStuff(firstVs, secondVs, secondNotVs, firstNotVs);

            //This should work (and it does)
            PrintStuff(vs1: firstVs, vs2: secondVs, nvs2: secondNotVs, nvs1: firstNotVs);

            //This shouldn't work (and it doesn't)
            //  PrintStuff(nvs2: secondVs, nvs1: firstVs, vs1: firstNotVs, vs2: secondNotVs);

            //This shouldn't work (and it doesn't)
            //PrintStuff(vs1: firstNotVs, vs2: secondNotVs, nvs1: firstVs, nvs2: secondVs);

            //This should work
            PrintStuff(nvs2: firstNotVs, nvs1: secondNotVs, vs1: firstVs, vs2: secondVs);

            //This should work
            PrintStuff(vs1: firstVs, nvs2: firstNotVs, vs2: secondVs, nvs1: secondNotVs);
        }

    }
}
