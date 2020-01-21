using System;
using DotNetVault.TestCaseHelpers;

namespace DotNetVault.Test.TestCases
{
    static class NdiNotOkCaseTwo
    {
        public static void NotOkNdiTest()
        {
            using (var chris = Name.CreateName("chris"))
            {
                Console.WriteLine(chris.Text);
                chris.Dispose();
                Console.WriteLine(chris.Text);
            }
            
        }
    }
}