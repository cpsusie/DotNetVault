using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public sealed class Bug64DemoCounterpoint1
    {
        /// <summary>
        /// Should need to comment out set to compile
        /// (Currently works correctly)
        /// </summary>
        public DateTime TimeStamp
        {
            get;
            //set;
        }

        /// <summary>
        /// Should need to comment out set to compile
        /// (Currently works correctly)
        /// </summary>
        public string StatusText
        {
            get;
            //set;
        }

        public Bug64DemoCounterpoint1(DateTime ts, string text)
        {
            TimeStamp = ts;
            StatusText = text;
        }
    }
}
