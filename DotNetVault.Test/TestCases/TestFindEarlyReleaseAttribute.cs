using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    public sealed class EarlyReleaseMethodHaver : IDisposable
    {
        [EarlyRelease]
        public void ReleaseEarly() { Console.WriteLine(@"Releasing early.");}

        public void Dispose()
        {

        }
    }

    public static class TestFindEarlyReleaseAttribute
    {
        [EarlyReleaseJustification(EarlyReleaseReason.CustomWrapperDispose)]
        public static void TestCase()
        {
            EarlyReleaseMethodHaver haver = new EarlyReleaseMethodHaver();
            haver.ReleaseEarly();
        }
    }
}
