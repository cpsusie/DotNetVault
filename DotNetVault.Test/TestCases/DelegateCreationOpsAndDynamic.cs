using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;


namespace DotNetVault.Test.TestCases
{
    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TResult>(in TInput input);

    [NoNonVsCapture]
    public delegate TResult LocalQuery<TInput, [VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
        in TInput input, in TAncillary ancillary);
    class DelegateCreationOpsAndDynamic
    {
        public static void ShouldNotBeAbleToDoThisEither()
        {
            LocalQuery<StringBuilder, dynamic, string> vq = (in StringBuilder res, in dynamic d) =>
            {
                res.AppendLine(d.ToString());
                return res.ToString();
            };
            StringBuilder sb = new StringBuilder();
            dynamic timeStamp = DateTime.Now;

            string result = vq(in sb, in timeStamp);
            Console.WriteLine(result);
        }

        private static string Execute(LocalQuery<StringBuilder, dynamic, string> executeMe, StringBuilder sb, dynamic d)
        {
            return executeMe(in sb, in d);
        }
    }
}
