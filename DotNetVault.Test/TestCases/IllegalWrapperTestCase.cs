using System;
using DotNetVault.Vaults;
using RwLck = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRwLockedResource;
namespace DotNetVault.Test.TestCases
{
    public ref struct IllegalWrapper
    {
        public static IllegalWrapper CreateCopyInSneakyWay(in RwLck lck, DateTime ts) => new IllegalWrapper(in lck, ts);

        public RwLck WrappedLockedResource;
        public DateTime Ts;

        public void SetLockedResource(in RwLck lck) => WrappedLockedResource = lck;

        private IllegalWrapper(in RwLck lck, DateTime ts)
        {
            WrappedLockedResource = lck; //the illegal copy happens here! naughty ... naughty!
            Ts = ts;
        }
    }

    static class IllegalWrapperTestCase
    {

        public static void HelpMe()
        {
            IllegalWrapper wrapper = default;
            {
                using RwLck lck = TheVault.Lock();
                
                //bad
                wrapper = IllegalWrapper.CreateCopyInSneakyWay(in lck, DateTime.Now);
                
                //also bad
                wrapper.SetLockedResource(in lck); 

                lck.AppendLine("Hi mom!");
            }
            Console.WriteLine(wrapper.WrappedLockedResource.ToString());
        }

        public static void AlsoNeedHelp()
        {
            IllegalWrapper wrapper = default;
            using (RwLck lck = TheVault.Lock()) 
            {
                wrapper.SetLockedResource(in lck); 
                lck.AppendLine("Goodbye cruel world!");
            }
            Console.WriteLine(wrapper.WrappedLockedResource.ToString());
            
        }

        private static readonly ReadWriteStringBufferVault TheVault =
            new ReadWriteStringBufferVault(TimeSpan.FromMilliseconds(750));
    }
}
