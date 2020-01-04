using System;
using DotNetVault.TestCaseHelpers;

namespace DotNetVault.Test.TestCases
{
    class EmitsDiagnosticUmRequiresInlineDecl
    {
        public static void EmitsDiagnosticNotDeclaredInline()
        {
            RefStructsRoxor disposeMe;
            using (disposeMe = 
                MethodsWithAndWithoutUsingMandatoryAttribute.CreateDisposableRefStruct())
            {
                Console.WriteLine(disposeMe.IsValid);
            }
            Console.WriteLine(disposeMe.IsValid);
        }

        public static void NoDiagnosticDeclaredInline()
        {
            //RefStructsRoxor disposeMe;
            using (var disposeMe =  
                MethodsWithAndWithoutUsingMandatoryAttribute.CreateDisposableRefStruct()) 
            {
                Console.WriteLine(disposeMe.IsValid);
            }
            Console.WriteLine(disposeMe.IsValid);
        }

        public static void NoDiagnosticDecl()
        {
            using var disposeMe = MethodsWithAndWithoutUsingMandatoryAttribute.CreateDisposableRefStruct();  
            Console.WriteLine(disposeMe.IsValid);
        }

    }
}
