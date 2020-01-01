using System;
using DotNetVault.TestCaseHelpers;

namespace DotNetVault.Test.TestCases
{
    public static class DiagnosticALittleMoreComplex
    {
        public static void TestMethod()
        {
            using (var disposable = new DisposableDooHickey())
            {
                Console.WriteLine(disposable.IsValid);
                var shouldBeHereToo = MethodsWithAndWithoutUsingMandatoryAttribute.CreateDisposableRefStruct();
                Console.WriteLine(shouldBeHereToo.IsValid && !shouldBeHereToo.IsDisposed);
            }
        }
    }

    internal sealed class DisposableDooHickey : IDisposable
    {
        public bool IsValid => !_flag.IsSet;

        public bool IsDisposed => _flag.IsSet;

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && _flag.TrySet())
            {

            }
        }

        private readonly SetOnceFlag _flag = new SetOnceFlag();
    }
}
