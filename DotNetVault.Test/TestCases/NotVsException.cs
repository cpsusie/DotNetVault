
using System;
using System.Text;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    class NotVsException : Exception
    {
        public sealed override string Message => _sb.ToString();

        private readonly StringBuilder _sb = new StringBuilder();
    }
    [VaultSafe]
    sealed class NotVsBcBase : NotVsException
    {

    }
}
