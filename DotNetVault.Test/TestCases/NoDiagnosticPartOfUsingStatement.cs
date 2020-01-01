using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetVault.Test.TestCases
{
    public static class NoDiagnosticPartOfUsingStatement
    {
        public static void GetUmMandatoryObject()
        {
            using (var disposeMe =
                TestCaseHelpers.MethodsWithAndWithoutUsingMandatoryAttribute.CreateDisposableRefStruct())
            {
                Console.WriteLine(disposeMe.IsValid);
            }
        }
    }
}
