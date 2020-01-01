using System;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TResult>(in TInput input);
    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
        in TInput input, in TAncillary ancillary);
    public sealed class QueryTestCases
    {
        public void TestMethod()
        {

            Console.WriteLine(@"Length times factor: {0}", GetStringLengthTimesFactor(4));
            Console.WriteLine(@"Text after appending: [{0}].", GetTextAfterAppend("Oh my goodness gracious me!"));
        }

        public int GetStringLengthTimesFactor(int factor)
        {

            return ExecuteQuery((in StringBuilder sb, in int f) => sb.Length * f, _sb, factor);
        }

        public int GetStringLengthTimesFactorV2(int factor)
        {
            return ExecuteQuery(GetLengthTimesFactor, in _sb, in factor);
        }

        public string GetTextAfterAppend([NotNull] string appendMe)
        {
            return ExecuteQuery(GetAfterAppendText, in _sb, in appendMe);
        }

        public StringBuilder BadNaughtyLeakContents()
        {
            return ExecuteQuery((in StringBuilder input, in string anc) =>
            {
                input.Append(anc);
                return input;
            }, _sb, "Malus, malus, malus");
        }

        //public QueryTestCases() { }

        private static string GetAfterAppendText(in StringBuilder sb, in string s) => sb + "s";

        private static int GetLengthTimesFactor(in StringBuilder sb, in int factor) => sb.Length * factor;

        

        private static StringBuilder IllegalGetTest(in StringBuilder sb, in string s)
        {
            sb.Append(s);
            return sb;
        }

        private static TResult ExecuteQuery<TInput, TAncillary, TResult>(
            [NotNull] LocalQuery<TInput, TAncillary, TResult> query,
            [NotNull] in TInput input, in TAncillary ancillary) => query(in input, in ancillary);
        

        private static TResult ExecuteQuery<TInput, TResult>([NotNull] LocalQuery<TInput, TResult> query,
            in TInput sb) => query(in sb);

        private readonly StringBuilder _sb = new StringBuilder("CLORTON! Muhahaahaha...");
    }
}
