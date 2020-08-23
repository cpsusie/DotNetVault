using System;
using System.Text;
using DotNetVault.CustomVaultExamples.CustomLockedResources;
using DotNetVault.Vaults;
using RoLock = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRoLockedResource;
using UpRoLock = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderUpgradableRoLockedResource;
using RwLock = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRwLockedResource;

namespace DotNetVault.Test.TestCases
{


    static class IllegalWrapperTestCase4
    {
        internal static void Bug92StillAProblem()
        {
            using var lck = TheVault.Lock();
            lck.AppendLine("We don't need no education ... ");

            //Bug 92 fix -- uncommenting the following line triggers compilation error
            //bug 92 fix -- a ref struct that has a non-static field of the same type as a locked
            //bug 92 fix -- resource object, may not appear in the same scope as the locked
            //bug 92 fix -- resource object, hence, following line will cause compilation error.
            RwLockWrapper wrapperCopy =  new RwLockWrapper(in lck, "Foobar");
            var secondWrapperCopy = new RwLockWrapper(in lck, "Fooz");
           // Console.WriteLine(wrapperCopy.ToString());
        }

        /// <summary>
        /// Locked resource objects should never be passed by value ... takes too long and
        /// is dangerous
        /// </summary>
        internal static void ShowPassByValueNowForbidden()
        {
            using (RoLock lck = TheVault.RoLock())
            {
                //Pass by value... bad ... now will not compile
                //Extensions.BadPrint3(lck);
                //passed by mutable reference ... now will not compile
                //Extensions.BadPrint4(ref lck);
                //passed by write-mandatory mutable reference ... now will not compile
                //Extensions.BadPrint5(out lck);

                //this is ok -- passed with 'in'
                Extensions.OkPrint(in lck);

                //you will always have to use in parameter... this won't compile
                //Extensions.OkPrint(lck);
                //this is fine too ... passed by 'in'
                //Extensions.OkPrint("Toodles", in lck);
                //it still catches non-in passing if name-colon syntax used ... now will not compile
                //Extensions.OkPrint(roLock: lck, text: "tiddly winks");
                //of course if you use 'in' with the name colon it's ok
                Extensions.OkPrint(roLock: in lck, text: "tiddly winks");

                //Still works if extension method ...
                //Can't pass by value to extension method ... will no longer compile
                //lck.BadPrint();
                //can't pass by mutable reference to extension method
                //lck.BadPrint2(); //will no longer compile

                //This is the correct way for extension method!
                //extension method with signature: 
                //"void OkPrintToo(this in RoLock roLock);"
                lck.OkPrintToo();
            }

            {
                //all same problems detected if using declaration
                using RoLock lck = TheVault.RoLock();

                //Pass by value... bad ... now will not compile
                //Extensions.BadPrint3(lck);
                //passed by mutable reference ... now will not compile
                //Extensions.BadPrint4(ref lck);
                //passed by write-mandatory mutable reference ... now will not compile
                //Extensions.BadPrint5(out lck);

                //this is ok -- passed with 'in'
                Extensions.OkPrint(in lck);

                //you will always have to use in parameter... this won't compile
                //Extensions.OkPrint(lck);
                //this is fine too ... passed by 'in'
                Extensions.OkPrint("Toodles", in lck);
                //it still catches non-in passing if name-colon syntax used ... now will not compile
                //Extensions.OkPrint(roLock: lck, text: "tiddly winks");
                //of course if you use 'in' with the name colon it's ok
                Extensions.OkPrint(roLock: in lck, text: "tiddly winks");

                //Still works if extension method ...
                //Can't pass by value to extension method ... will no longer compile
                //lck.BadPrint();
                //can't pass by mutable reference to extension method
                //lck.BadPrint2(); //will no longer compile

                //This is the correct way for extension method!
                //extension method with signature: 
                //"void OkPrintToo(this in RoLock roLock);"
                lck.OkPrintToo();

            }
        }

        public static RwLock SneakyCopy(in RwLock item) => item;

        private static readonly ReadWriteStringBufferVault TheVault =
            new ReadWriteStringBufferVault(TimeSpan.FromMilliseconds(250), () => new StringBuilder(10_000));

    }

    ref struct RwLockWrapper
    {
        public RwLock Lock;
        public string Text;

        public RwLockWrapper(in RwLock lck, string text)
        {
            Lock = lck;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public new string ToString() => "Lock [" + Lock.ToString() + "].";
    }

    internal static class Extensions
    {
        public static void BadPrint(this RoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint2(this ref RoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint3(RoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint4(ref RoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint5(out RoLock lck)
        {
            lck = new StringBuilderRoLockedResource();
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint(this UpRoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint2(this ref UpRoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint(string text, RoLock roLock)
        {
            Console.WriteLine(text + roLock.ToString());
        }

        public static void OkPrint(string text, in RoLock roLock)
        {
            Console.WriteLine(text + roLock.ToString());
        }

        public static void OkPrint(in RoLock roLock)
        {
            Console.WriteLine(roLock.ToString());
        }

        public static void OkPrintToo(this in RoLock roLock)
        {
            Console.WriteLine(roLock.ToString());
        }

        public static void BadPrint3(UpRoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint4(ref UpRoLock lck)
        {
            Console.WriteLine(lck.ToString());
        }

        public static void BadPrint5(out UpRoLock lck)
        {
            lck = new UpRoLock();
            Console.WriteLine(lck.ToString());
        }

    }


}
