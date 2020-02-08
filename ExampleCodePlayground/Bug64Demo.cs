using System;
using DotNetVault.Attributes;

namespace ExampleCodePlayground
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
        /// Bug 64 FIX: NO LONGER NEED TO COMMENT IT OUT! (Intellisense still seems to think I do,
        /// Bug 64 but it builds.  Bizarre.) ... perhaps submit bug to ms
        /// Bug 64 Intellisense Note: intellisense does not seem to catch up with the compiler
        /// bug 64 after installing fixed library/analyzer until restarting visual studio
        /// 
        /// </summary>
        public string StatusText
        {
            get; 
            set;
        }
    }


    [VaultSafe]
    public sealed class Bug64DemoCounterpoint
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
            //BUG 64 COMMENT -- if this were a STRUCT, the setter would be ok 
            //Bug 64 COMMENT -- since this is a reference type, it must be 100% immutable
            //Bug 64 COMMENT -- all the way through its graph
            //Bug 64 COMMENT -- otherwise, a copy to this object that escaped from the vault
            //Bug 64 COMMENT -- could be used to change the value in the vault without synchronization
            get; 
            //set;
        }

        public Bug64DemoCounterpoint(DateTime ts, string text)
        {
            TimeStamp = ts;
            StatusText = text;
        }
    }

    [VaultSafe]
    public struct SecondCounterPointToBug64
    {
        /// <summary>
        /// Should NOT need to comment out setter to compile (and currently do not)
        /// </summary>
        public DateTime Timestamp
        {
            get;
            set;
        }

        //Bug 64 FIX-- A mutable reference type (like status text)
        //Bug 64 FIX-- cannot appear AT ALL in a vault safe struct no get no set
        //Bug 64 FIX-- an IMMUTABLE reference type like string can have both getters or setters 
        //Bug 64 FIX-- if has reference type owners anywhere back in the graph (so ok here)
        //Bug 64 FIX-- Swap the property from the string to StringBuilder version (and 
        //Bug 64 FIX-- and do same in CTOR to verify 
        //public StringBuilder StatusText { get; }
        public string StatusText { get; set; }

        public SecondCounterPointToBug64(string text)
        {
            //
            //StatusText = new StringBuilder(text);
            StatusText = text;
            
            Timestamp = DateTime.Now;
        }
    }
}
