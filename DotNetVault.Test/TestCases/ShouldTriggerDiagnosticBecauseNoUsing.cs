using System;

namespace DotNetVault.Test.TestCases
{
    public static class Test
    {
        public static void GetUmMandatoryObject()
        {
            var disposeMe =
                TestCaseHelpers.MethodsWithAndWithoutUsingMandatoryAttribute.CreateDisposableRefStruct();
            Console.WriteLine(disposeMe.IsValid);
        }
    }
    
}
