using System;
using System.Collections.Immutable;
using System.Text;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace ExampleCodePlayground
{
    public static class Bug62TestCases
    {
        internal static MutableResourceVault<StringBuilder> CreateMutableResourceVault() =>
            MutableResourceVault<StringBuilder>.CreateMutableResourceVault(() => new StringBuilder(),
                TimeSpan.FromMilliseconds(100));

        internal static ImmutableArray<string> BunchOfStrings => TheStrings;

        public static void ExecuteDemonstrationMethods()
        {
            Console.WriteLine();
            Console.WriteLine("BEGINNING BUG 62 TEST CASES");
            ThisMethodCorrectlyWillRaiseDiagnosticNow();
            ThisMethodDoesNotRaiseDiagnosticRightNowButShould();
            Console.WriteLine("ENDING BUG 62 TEST CASES");
            Console.WriteLine();
        }

        public static void ThisMethodCorrectlyWillRaiseDiagnosticNow()
        {
            const string methodName = nameof(ThisMethodCorrectlyWillRaiseDiagnosticNow);
            string finalResult;
            using (var mrv = CreateMutableResourceVault())
            {
                using var lck = mrv.Lock();
                lck.ExecuteAction((ref StringBuilder sb) => sb.AppendLine("Hello, world."));

                //bug 62 Following two lines rightly will not compile.
                //lck.ExecuteAction((ref StringBuilder sb) =>
                //    StringBuilderExtensions.AppendRangeToStringBuilder(sb, BunchOfStrings));

                finalResult = lck.ExecuteQuery((in StringBuilder sb) => sb.ToString());
            }
            Console.WriteLine($"Result from [{methodName}]: [{finalResult}]");
        }

        public static void ThisMethodDoesNotRaiseDiagnosticRightNowButShould()
        {
            const string methodName = nameof(ThisMethodDoesNotRaiseDiagnosticRightNowButShould);
            string finalResult;
            using (var mrv = CreateMutableResourceVault())
            {
                using var lck = mrv.Lock();
                lck.ExecuteAction((ref StringBuilder sb) => sb.AppendLine("Hello, world."));

                //bug 62 The following line WILL compile but should not
                //BUG 62 FIXED ! Following Line no longer compiles.
                //lck.ExecuteAction((ref StringBuilder sb) => sb.AppendRange(BunchOfStrings));

                //post bug 62 fix, demonstrate acceptable alternative
                lck.ExecuteAction((ref StringBuilder sb, in ImmutableArray<string> au) =>
                {
                    foreach (var str in au)
                    {
                        sb.AppendLine(str);
                    }
                }, BunchOfStrings);

                finalResult = lck.ExecuteQuery((in StringBuilder sb) => sb.ToString());
            }
            Console.WriteLine($"Result from [{methodName}]: [{finalResult}]");
        }

        private static readonly ImmutableArray<string> TheStrings = (new[] {"Abercrombie", "Benjamin", "Christoper"}).ToImmutableArray();
    }

    public static class StringBuilderExtensions
    {
        public static void AppendRangeToStringBuilder([NotNull] StringBuilder sb, ImmutableArray<string> appendUs)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            foreach (var str in appendUs)
            {
                if (str == null) throw new ArgumentException(@"One or more null strings in parameter.",
                    nameof(appendUs));
                sb.AppendLine(str);
            }
        }

        public static void AppendRange(this StringBuilder sb, ImmutableArray<string> appendUs)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            foreach (var str in appendUs)
            {
                if (str == null) throw new ArgumentException(@"One or more null strings in parameter.",
                    nameof(appendUs));
                sb.AppendLine(str);
            }
        }
    }
}
