using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public struct Bug64Demo
    {
        /// <summary>
        /// Should NOT need to comment out setter to compile (and currently do not)
        /// </summary>
        public DateTime Timestamp
        {
            get; 
            set;
        }

        /// <summary>
        /// Should NOT need to comment out  setter to compile (but currently do)
        /// </summary>
        public string StatusText
        {
            get; 
            set;
        }
    }

  
}

//[VaultSafe]
//public struct Bug64Demo
//{
//    /// <summary>
//    /// Should NOT need to comment out setter to compile (and currently do not)
//    /// </summary>
//    public DateTime Timestamp
//    {
//        get;
//        set;
//    }

//    /// <summary>
//    /// Should NOT need to comment out  setter to compile (but currently do)
//    /// </summary>
//    public string StatusText
//    {
//        get;
//        //set;
//    }
//}

//[VaultSafe]
//public sealed class Bug64DemoCounterpoint
//{
//    /// <summary>
//    /// Should need to comment out set to compile
//    /// (Currently works correctly)
//    /// </summary>
//    public DateTime TimeStamp
//    {
//        get;
//        //set;
//    }

//    /// <summary>
//    /// Should need to comment out set to compile
//    /// (Currently works correctly)
//    /// </summary>
//    public string StatusText
//    {
//        get;
//        //set;
//    }

//    public Bug64DemoCounterpoint(DateTime ts, string text)
//    {
//        TimeStamp = ts;
//        StatusText = text;
//    }
//}
