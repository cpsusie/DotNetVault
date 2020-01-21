using System;
using DotNetVault.TestCaseHelpers;

namespace DotNetVault.Test.TestCases
{
    public class TestNdiOk
    {
        public static void TestNdiOkNoDirectInvoke()
        {
            using var chris = Name.CreateName("chris");
            Console.WriteLine(chris.Text);
        }

        public static void TestAlsoOkNoDirectInvoke()
        {
            using (var chris = Name.CreateName("chris"))
            {
                Console.WriteLine(chris.Text);
            }
        }
    }
}
