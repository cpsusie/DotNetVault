using System;
using DotNetVault.Vaults;
using RwLock = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRwLockedResource;
namespace DotNetVault.Test.TestCases
{
    ref struct RwLockWrapper
    {
        public RwLock WrappedLock { get; }
        public string Name { get; }

        public RwLockWrapper(in RwLock lck, string text)
        {
            WrappedLock = lck;
            Name = text ?? string.Empty;
        }

        public new readonly string ToString() => WrappedLock.ToString();
    }
    static class IllegalWrapperTestCase3
    {
        internal static void ShowMoreDetailedBug92Fix()
        {
            //protected resources are now decorated with the NoCopy attribute.  
            //If a ref struct contains a field of the same type as the protected resource,
            //it is now prohibited to allow these two to overlap in scope.

            //ref struct wrapper with containing non-static field of type StringBuilderRwLockedResource 
            string fizz;
            {
                using var lck = TheVault.Lock();
                lck.AppendLine("We don't need no education ... ");

                //Bug 92 fix -- uncommenting the following line triggers compilation error
                //bug 92 fix -- a ref struct that has a non-static field of the same type as a locked
                //bug 92 fix -- resource object, may not appear in the same scope as the locked
                //bug 92 fix -- resource object, hence, following line will cause compilation error.
                RwLockWrapper wrapperCopy = new RwLockWrapper(in lck, "Foobar");
                fizz = wrapperCopy.ToString();
            }
            //the locked resource has been returned to vault and this line causes unsynchronized access
            Console.WriteLine(fizz);
        }
        private static readonly ReadWriteStringBufferVault TheVault =
            new ReadWriteStringBufferVault(TimeSpan.FromMilliseconds(750));
    }
}
