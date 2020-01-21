using System;
using DotNetVault.TestCaseHelpers;

namespace DotNetVault.Test.TestCases
{
    static class NdiNotOkCaseOne
    {
        public static void NotOkNdiTest()
        {
            using var chris = Name.CreateName("chris");
            chris.Dispose();
            Console.WriteLine(chris.Text);
        }
    }
}
