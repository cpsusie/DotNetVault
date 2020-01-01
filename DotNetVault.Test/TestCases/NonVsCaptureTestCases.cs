using System;
using System.Text;
using DotNetVault.Attributes;
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
        public StringBuilder WilyLittleBastard
        {
            get => _wilyBastard;
            set => _wilyBastard = value;
        }
        
        public void TestMethod()
        {

            Console.WriteLine(@"Length times factor: {0}", GetStringLengthTimesFactor(4));
            Console.WriteLine(@"Text after appending: [{0}].", GetTextAfterAppend("Oh my goodness gracious me!"));
        }

        public int GetStringLengthTimesFactor(int factor)
        {

            return ExecuteQuery((in StringBuilder sb, in int f) => sb.Length * f, _sb, factor);
        }

        public int GetStringLengthTimesFactorV3(int factor)
        {
            LocalQuery<StringBuilder, int, int> notLazyFromLambda =
                new LocalQuery<StringBuilder, int, int>((in StringBuilder sb, in int f) => sb.Length * f);
            return ExecuteQuery(notLazyFromLambda, _sb, factor);
        }

        public int GetStringLengthTimesFactoryV4(int factor)
        {
            LocalQuery<StringBuilder, int, int> notLazyFromLambda =
                new LocalQuery<StringBuilder, int, int>(GetLengthTimesFactor);
            return ExecuteQuery(notLazyFromLambda, _sb, factor);
        }

        public int GetStringLengthTimesFactorV2(int factor)
        {
            return ExecuteQuery(GetLengthTimesFactor, in _sb, in factor);
        }

        public string ExecuteQueryFromElseWhere([NotNull] LocalQuery<StringBuilder, string, string> query, string appendMe)
        {
            return query(in _sb, in appendMe);
        }

        public string GetTextAfterAppend([NotNull] string appendMe)
        {
            return ExecuteQuery(GetAfterAppendText, in _sb, in appendMe);
        }

        public string WilyBastardGetTextAfterAppend([NotNull] string appendMe)
        {
            LocalQuery<StringBuilder, string, string> sneakyLilLambda = (in StringBuilder sb, in string s) =>
                {
                    sb.Append(s);
                    WilyLittleBastard = sb;
                    return sb.ToString();
                };
            return ExecuteQuery(sneakyLilLambda, in _sb, in appendMe);
        }

        

        
       
        //public QueryTestCases() { }

        private static string GetAfterAppendText(in StringBuilder sb, in string s)
        {
            if (sb != null && s != null)
            {
                return sb + "s";
            }
            if (sb == null && s != null)
            {
                return string.Empty + s;
            }
            return "emptification";

        }

        private static int GetLengthTimesFactor(in StringBuilder sb, in int factor) => sb.Length * factor;
        

        private static TResult ExecuteQuery<TInput, [VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
            [NotNull] LocalQuery<TInput, TAncillary, TResult> query,
            [NotNull] in TInput input, in TAncillary ancillary) => query(in input, in ancillary);


        internal static TResult ExecuteQuery<TInput, [VaultSafeTypeParam] TResult>([NotNull] LocalQuery<TInput, TResult> query,
            in TInput sb) => query(in sb);

        private readonly StringBuilder _sb = new StringBuilder("CLORTON! Muhahaahaha...");

        private volatile StringBuilder _wilyBastard = new StringBuilder(
            "I'm a wily bastard of a type who isn't vault safe and I'm gonna try to sneak my way in to leak resources out of the vault.");
    }


    public static class AnotherClassTryingToDoSomeSneakyAssShit
    {
        public static string ExecuteSneakyAssShit()
        {
            QueryTestCases cases = new QueryTestCases();
            return cases.ExecuteQueryFromElseWhere(SneakyLilBastardSusceptible, "FOOOOOBARRRR!!!!");
        }

        private static string SneakyLilBastardSusceptible(in StringBuilder sb, in string s)
        {
            sb.Append(s);
            _sb = sb;
            return sb.ToString();
        }

        private static volatile StringBuilder _sb = new StringBuilder();
    }
}
