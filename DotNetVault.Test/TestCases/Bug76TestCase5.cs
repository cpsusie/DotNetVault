using System.Text;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    /// <summary>
    /// Execute a potentially mutating action on the vault
    /// </summary>
    /// <typeparam name="TResource">the type of protected resource you wish to mutate</typeparam>
    /// <typeparam name="TAncillary">an ancillary type used in the delegate.  must be vault safe, must be passed
    /// by readonly-reference.</typeparam>
    /// <param name="res">the protected resource on which you wish to perform a mutation.</param>
    /// <param name="ancillary">the ancillary object</param>
    /// <remarks>See <see cref="NoNonVsCaptureAttribute"/> and the limitations it imposes on the semantics of
    /// delegates so-annotated.</remarks>
    [NoNonVsCapture]
    public delegate void VaultAction<TResource, [VaultSafeTypeParam] TAncillary>(ref TResource res,
        in TAncillary ancillary);

    class Bug76TestCase5
    {
        //should not work
        public void TestExplicitDelegateNonStatic()
        {
            var executor = new VaultActionExecutor();

            VaultAction<StringBuilder, string> vaDel = delegate(ref StringBuilder sb, in string s)
            {
                sb.AppendLine(s);
                //incorrect, references "this" and "_sb"
                _sb = sb;
            };

            executor.ExecuteAction(vaDel, "Hi mom!");
        }

        //should not work
        public void TestExplicitDelegateStatic()
        {
            var executor = new VaultActionExecutor();
            VaultAction<StringBuilder, string> vaDel = delegate (ref StringBuilder sb, in string s)
            {
                sb.AppendLine(s);
                //incorrect, references "this" and "_sb"
                StaticStringBuilder = sb;
            };

            executor.ExecuteAction(vaDel, "Hi mom!");
        }

        //should not work
        public void TestExplicitDelegateNonStatic2()
        {
            var executor = new VaultActionExecutor();

            VaultAction<StringBuilder, string> vaDel = (ref StringBuilder sb, in string s) =>
            {
                sb.AppendLine(s);
                //incorrect, references "this" and "_sb"
                _sb = sb;
            };

            executor.ExecuteAction(vaDel, "Hi mom!");
        }

        //should not work
        public void TestExplicitDelegateStatic2()
        {
            var executor = new VaultActionExecutor();
            VaultAction<StringBuilder, string> vaDel = (ref StringBuilder sb, in string s) =>
            {
                sb.AppendLine(s);
                //incorrect, references "this" and "_sb"
                StaticStringBuilder = sb;
            };

            executor.ExecuteAction(vaDel, "Hi mom!");
        }

        //should not work
        public void TestExplicitDelegateStatic3()
        {
            var executor = new VaultActionExecutor();
            VaultAction<StringBuilder, string> vaDel = StaticAppendText;
            executor.ExecuteAction(vaDel, "Hi mom!");
        }

        private static string StaticGetText(in StringBuilder sb)
        {
            StaticStringBuilder = sb;
            return sb.ToString();
        }

        private string GetText(in StringBuilder res)
        {
            _sb = res;
            return res.ToString();
        }

        private static void StaticAppendText(ref StringBuilder sb, in string appendMe)
        {
            StaticStringBuilder = sb;
            sb.AppendLine(appendMe);
        }

        private void AppendText(ref StringBuilder protectedRes, in string appendMe)
        {
            _sb = protectedRes;
            protectedRes.AppendLine(appendMe);
        }



        private StringBuilder _sb = new StringBuilder();
        private static StringBuilder StaticStringBuilder = new StringBuilder();
    }

    class VaultActionExecutor
    {
        public void ExecuteAction([NotNull] VaultAction<StringBuilder, string> del, in string text)
        {
            del(ref _sb, text);
        }

        private StringBuilder _sb = new StringBuilder();
    }
}
