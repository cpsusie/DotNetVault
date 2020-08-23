using System;
using System.Text;
using DotNetVault.CustomVaultExamples.CustomLockedResources;
using DotNetVault.Vaults;
using RoLock = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRoLockedResource;
using UpRoLock = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderUpgradableRoLockedResource;
using RwLock = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRwLockedResource;

namespace ExampleCodePlayground
{
    

    static class Bug92Demo
    {

        //Bug 92 -- it was possible to copy a locked resource object protected by using 
        //bug 92 -- into another locked resource object with greater/longer scope, then
        //bug 92 -- use the copy assignment target for unsynchronized access to protected resource, 
        internal static void OldShowBug92Fix()
        {
            //Declare some objects that have longer scope than the locked resource objects
            //declared in block after these
            string fooz;
            RwLock copy;
            RwLock yetAgainACopy;
            string fizz;
            {
                 using RwLock lck = default;//... everything will compile but probably crash so don't run this method.
                //swapping with the assignment below("using RwLock lck = TheVault.Lock();")
                //lots of stuff (correctly) refuses compile
                //Need to have an actual locked resource 
                //gotten from a vault with using mandatory etc to poison this scope from
                //shenanigans
                
                //presence of this protected resource in this scope prevents presence of other 
                //protected resources of the same type in this scope unless they are obtained
                //via a Lock/SpinLock method and themselves properly protected.
                //using RwLock lck = TheVault.Lock();
                lck.AppendLine("Hello, world!");

                //Artificially increase scope of lck NOW WILL NOT COMPILE
                //OK next till not compile
                using (copy = lck)
                {
                    fooz = copy.ToString();
                }

                //OK next will not compile
                RwLock anotherCopy = lck;
                //OK next till not compile
                RwLock stillAnotherCopy = SneakyCopy(in lck);
                //nor will this
                using RwLock tired = SneakyCopy(in lck);

                //FINALLY ... won't compile :)
                yetAgainACopy = SneakyCopy(in lck);

                using (yetAgainACopy = SneakyCopy(in lck))
                {
                    fooz = yetAgainACopy.ToString();
                }

                //or more sneakily //NOW WILL NOT COMPILE
               RwLockWrapper wrapperCopy = new RwLockWrapper(in lck, "hi");
                
               fizz = wrapperCopy.Text;
                
                
            }
            //BOTH ACCESS WOULD BE UNSYNCHRONIZED if the lines in block above
            //were uncommented and allowed to compile
            //Console.Write(copy.ToString());
            //Console.WriteLine(wrapperCopy.Lock.ToString());
            //Console.WriteLine(yetAgainACopy.Length);
            Console.WriteLine(fizz);
            Console.WriteLine(fooz);
        }


        //internal static void ShowMoreDetailedBug92Fix()
        //{
        //    //protected resources are now decorated with the NoCopy attribute.  
        //    //If a ref struct contains a field of the same type as the protected resource,
        //    //it is now prohibited to allow these two to overlap in scope.
            
        //    //ref struct wrapper with containing non-static field of type StringBuilderRwLockedResource 
        //    RwLockWrapper wrapperCopy=default;
        //    {
        //        using (var lck = TheVault.Lock())
        //        {
        //            lck.AppendLine("We don't need no education ... ");

        //            //Bug 92 fix -- uncommenting the following line triggers compilation error
        //            //bug 92 fix -- a ref struct that has a non-static field of the same type as a locked
        //            //bug 92 fix -- resource object, may not appear in the same scope as the locked
        //            //bug 92 fix -- resource object, hence, following line will cause compilation error.
        //            using (wrapperCopy = new RwLockWrapper(in lck, "Foobar"))
        //            {
        //                Console.WriteLine(wrapperCopy.ToString());
        //            }
        //        }
        //    }
        //    //the locked resource has been returned to vault and this line causes unsynchronized access
        //    Console.WriteLine(wrapperCopy.ToString());
        //}

        //internal static void ShowMoreDetailedBug92AnotherVariation()
        //{
        //    //protected resources are now decorated with the NoCopy attribute.  
        //    //If a ref struct contains a field of the same type as the protected resource,
        //    //it is now prohibited to allow these two to overlap in scope.

        //    //ref struct wrapper with containing non-static field of type StringBuilderRwLockedResource 
        //    string fizz;
        //    {
                
        //        using (var lck = TheVault.Lock())
        //        {
        //            lck.AppendLine("We don't need no education ... ");

        //            //Bug 92 fix -- uncommenting the following line triggers compilation error
        //            //bug 92 fix -- a ref struct that has a non-static field of the same type as a locked
        //            //bug 92 fix -- resource object, may not appear in the same scope as the locked
        //            //bug 92 fix -- resource object, hence, following line will cause compilation error.
        //            var wrapperCopy = new RwLockWrapper(in lck, "Foobar");
        //            fizz = wrapperCopy.ToString();
        //        }
        //    }
        //    //the locked resource has been returned to vault and this line causes unsynchronized access
        //    Console.WriteLine(fizz);
        //}

        internal static void Bug92StillAProblem()
        {
            using var lck = TheVault.Lock();
            lck.AppendLine("We don't need no education ... ");

            //Bug 92 fix -- uncommenting the following line triggers compilation error
            //bug 92 fix -- a ref struct that has a non-static field of the same type as a locked
            //bug 92 fix -- resource object, may not appear in the same scope as the locked
            //bug 92 fix -- resource object, hence, following line will cause compilation error.
            //using RwLockWrapper wrapperCopy = new RwLockWrapper(in lck, "Foobar");
            //Console.WriteLine(wrapperCopy.ToString());
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

        public void Dispose() { }
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
