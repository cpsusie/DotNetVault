using System;
using DotNetVault.TestCaseHelpers;

namespace DotNetVault.Test.TestCases
{
    static class NdiMixedWithNotInlineNotOkCaseThree
    {
        public static void NotOkNdiAndNotOkNotInlineTest()
        {
            Name chris;
            using (chris = Name.CreateName("chris"))
            {
                Console.WriteLine(chris.Text);
            }
            chris.Dispose();
            Console.WriteLine(chris.Text);

        }
    }
}