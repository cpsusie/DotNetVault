using System;
using System.Collections.Immutable;
using System.Text;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TResult>(in TInput input);

    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
        in TInput input, in TAncillary ancillary);

    [NoNonVsCapture]
    public delegate void
        LocalAction<TInput, [VaultSafeTypeParam] TAncillary>(ref TInput input, in TAncillary ancillary);

    public sealed class QueryTestCases
    {
        public void TestMethod()
        {
            StringBuilder tester = new StringBuilder();
            tester.AppendLine("Hello, world!");
            ImmutableArray<string> strings = new[] {"Abercrombie", "Benjamin", "Christopher"}.ToImmutableArray();
            LocalAction<StringBuilder, ImmutableArray<string>> action =
                (ref StringBuilder sb, in ImmutableArray<string> arr) =>
                {
                    StringBuilderUnitTestExtensions.UnitTestOnlyAppendRangeToStringBuilder(sb, arr);
                };
            ExecuteAction(action, ref tester, in strings);
            string finalResult = ExecuteQuery((in StringBuilder sb) => sb.ToString(), tester);
            Console.WriteLine(finalResult);
        }




        private static TResult ExecuteQuery<TInput, TAncillary, TResult>(
            [NotNull] LocalQuery<TInput, TAncillary, TResult> query,
            [NotNull] in TInput input, in TAncillary ancillary) => query(in input, in ancillary);

        private static void ExecuteAction<TInput, TAncillary>(LocalAction<TInput, TAncillary> action, ref TInput input,
            in TAncillary ancillary) => action(ref input, ancillary);

        private static TResult ExecuteQuery<TInput, TResult>([NotNull] LocalQuery<TInput, TResult> query,
            in TInput sb) => query(in sb);

        private readonly StringBuilder _sb = new StringBuilder("CLORTON! Muhahaahaha...");
    }

    public static class StringBuilderUnitTestExtensions
    {
        public static void UnitTestOnlyAppendRangeToStringBuilder([NotNull] StringBuilder sb, ImmutableArray<string> appendUs)
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