using System;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;

namespace ExampleCodePlayground
{
    //[ReportWhiteListLocations]
    sealed class Bug76Demo
    {
        //should not work
        public void TestFromAnotherFileStaticMethodGetTest()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    ////bug 76 following line uncommented now (rightly) causes compilation failure
                    //lck.ExecuteAction(Bug76DemoHelper.StaticAppendText, "Hi mom!");
                }
            }
        }
        //should not work
        public void TestNonStaticMethodGetTest()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    ////bug 76 following line uncommented now (rightly) causes compilation failure
                    //lck.ExecuteAction(AppendText, "Hi mom!");
                }
            }
        }


        //should not work
        public void TestStaticMethodGetTest()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    ////bug 76 following line uncommented now (rightly) causes compilation failure
                    //lck.ExecuteAction(StaticAppendText, "Hi mom!");
                }
            }
        }

        //should not work
        public void TestLambdaNonStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    lck.ExecuteAction((ref StringBuilder sb, in string s) =>
                    {
                        sb.AppendLine(s);
                        //bug 76 following line uncommented now (rightly) causes compilation failure
                        //_sb = sb;
                    }, "Hi mom!");
                }
            }
        }

        //should not work
        public void TestLambdaStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    lck.ExecuteAction((ref StringBuilder sb, in string s) =>
                    {
                        sb.AppendLine(s);
                        //correct, references Static StringBuilder
                        //bug 76 following line uncommented now (rightly) causes compilation failure
                        //StaticStringBuilder = sb;
                    }, "Hi mom!");
                }
            }
        }

        //should not work BUT DOES -- BUG 76 FIX -- no longer works
        public void TestDelegateNonStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    lck.ExecuteAction(delegate (ref StringBuilder sb, in string s) 
                    {
                        sb.AppendLine(s);
                        //BUG 76 FIX now correct, -- will not compile bc references "this" and "_sb"
                        //bug 76 following line uncommented now (rightly) causes compilation failure
                        //_sb = sb;
                    }, "Hi mom!");
                }
            }
        }

        //should not work BUT DOES -- BUG
        public void TestDelegateStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    lck.ExecuteAction(delegate (ref StringBuilder sb, in string s)
                    {
                        sb.AppendLine(s);
                        //BUG 76 FIX now correct, -- will not compile bc references STATIC STRING BUILDER
                        //bug 76 following line uncommented now (rightly) causes compilation failure
                        //StaticStringBuilder = sb;
                    }, "Hi mom!");
                }
            }
        }

        //should not work BUT DOES -- BUG 76 FIX -- now DOES NOT WORK (and rightly so)
        public void TestExplicitDelegateNonStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    VaultAction<StringBuilder, string> explicitDel = delegate(ref StringBuilder sb, in string s)
                    {
                        sb.AppendLine(s);
                        //BUG 76 FIX now correct, -- will not compile bc references "this" and "_sb"
                        //bug 76 following line uncommented now (rightly) causes compilation failure
                        //_sb = sb;
                    };
                    using var lck = vault.Lock();
                    lck.ExecuteAction(explicitDel, "Hi mom!");
                }
            }
        }

        //should not work BUT DOES -- BUG 76 FIX -- now DOES NOT WORK (and rightly so)
        public void TestExplicitDelegateStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    VaultAction<StringBuilder, string> explicitDel = delegate (ref StringBuilder sb, in string s)
                    {
                        sb.AppendLine(s);
                        //BUG 76 FIX now correct, -- will not compile bc references STATIC STRING BUILDER
                        //bug 76 following line uncommented now (rightly) causes compilation failure
                        //StaticStringBuilder = sb;
                    };
                    using var lck = vault.Lock();
                    lck.ExecuteAction(explicitDel, "Hi mom!");
                }
            }
        }
        public void TestLocalFunctionNonStatic2()
        {
            StringBuilder localSb = new StringBuilder();
            using (var vault = CreateSbVault())
            {
                {
                    //bug 76 fix -- now correct, does not work -- inter alia, references "localSb"
                    using var lck = vault.Lock();
                    lck.ExecuteAction(LocalAppendText, "Hi mom!");
                }
            }
            void LocalAppendText(ref StringBuilder protectedRes, in string appendMe)
            {
                //bug 76 following line uncommented now (rightly) causes compilation failure
                //localSb = protectedRes;
                protectedRes.AppendLine(appendMe);
            }
        }
        public void TestLocalFunctionNonStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    //bug 76 fix -- now correct, does not work -- inter alia, references "this"
                    using var lck = vault.Lock();
                    lck.ExecuteAction(LocalAppendText, "Hi mom!");
                }
            }
            void LocalAppendText(ref StringBuilder protectedRes, in string appendMe)
            {
                //bug 76 following line uncommented now (rightly) causes compilation failure
                //_sb = protectedRes;
                protectedRes.AppendLine(appendMe);
            }
        }

        //BUG 76 FIXED 
        public void TestLocalFunctionStatic()
        {
            using (var vault = CreateSbVault())
            {
                {
                    using var lck = vault.Lock();
                    //bug 76 fix -- now correct, does not work -- inter alia, references STATIC STRINGBUILDER
                    lck.ExecuteAction(LocalAppendText, "Hi mom!");
                }
            }
            static void LocalAppendText(ref StringBuilder protectedRes, in string appendMe)
            {
                //bug 76 following line uncommented now (rightly) causes compilation failure
                //StaticStringBuilder = protectedRes;
                protectedRes.AppendLine(appendMe);
            }
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

        static MutableResourceVault<StringBuilder> CreateSbVault() =>
            MutableResourceVault<StringBuilder>.CreateAtomicMutableResourceVault(() => new StringBuilder(),
                TimeSpan.FromMilliseconds(250));

        private StringBuilder _sb = new StringBuilder();
        private static StringBuilder StaticStringBuilder = new StringBuilder();
    }
}
