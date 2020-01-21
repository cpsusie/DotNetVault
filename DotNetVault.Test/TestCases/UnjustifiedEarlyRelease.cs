using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    public sealed class EarlyReleaseMethodHaver : IDisposable
    {
        public string Text { get; } = "Some text";
        [EarlyRelease]
        public void ReleaseEarly() { Console.WriteLine(@"Releasing early."); }

        public void Dispose()
        {

        }
    }

    public static class TestFindEarlyReleaseAttribute
    {
        [return: UsingMandatory]
        public static EarlyReleaseMethodHaver TestCase()
        {
            EarlyReleaseMethodHaver haver = new EarlyReleaseMethodHaver();
            try
            {
                Console.WriteLine(haver.Text);
                return haver;
            }
            catch (Exception)
            {
                haver.ReleaseEarly();
                throw;
            }
        }
    }
}