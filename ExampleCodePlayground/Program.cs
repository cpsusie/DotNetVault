using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace ExampleCodePlayground
{
    class Program
    {
        static void Main()
        {
            BigStructVaultExample.RunDemo();
            /**
            //bug 92 is fixed code should not be run but exits to demonstrate analyzer operations
            //uncomment designated code to get various correct compiler errors
            //Console.WriteLine("BEGIN BUG 92 DEMO");
            //Bug92Demo.ShowBug92Fix();
            //Bug92Demo.ShowPassByValueNowForbidden();
            //Console.WriteLine("END BUG 92 DEMO");
            Console.WriteLine();
            Console.WriteLine();

            UsingBigMutableStructExample.ReadOnlyAccess();
            UsingBigMutableStructExample.ReadWriteAccess();
            UsingBigMutableStructExample.UpgradableTest();

            MutableStructCareExamples.LargeMutableStructsAreNowEfficientProtectedResourcesWithReturnByReference();
            MutableStructCareExamples.DemonstrateNeedForRuleAgainstLocalRefAlias();
            KnownIssuesDemonstration.DemonstrateDoubleDispose();
            KnownIssuesDemonstration.DemonstrateBadExtensionMethod();
            KnownIssuesDemonstration.DemonstrateBadArrayBecauseTypeItselfInherentlyLeaksOwnState();
            StringBuilderCodeSamples.DemonstrateQueries();
            StringBuilderCodeSamples.DemonstrateActions();
            StringBuilderCodeSamples.DemonstrateMixedOperations();
            StringBuilderCodeSamples.DemonstrateUseOfExtensionMethodsToSimplify();
            ConvenienceWrappersDemo.ShowWrapperUsage();
            Bug62TestCases.ExecuteDemonstrationMethods();
           // TestUriIsVs();

            //Console.WriteLine("Hello World!");
            //Console.WriteLine();
            //Console.WriteLine("Doing mutable struct intricacies demo...");
            //MutableStructCareExamples.ThisMightNotWorkTheWayYouThink();
            //Console.WriteLine("Ending mutable struct intricacies.");

            //BasicVault<DateTime> theDt = new BasicVault<DateTime>(DateTime.Now, TimeSpan.FromMilliseconds(250));
            //using var lk = theDt.SpinLock();
            //Console.WriteLine("Protected timestamp: [{0:O}]", lk.Value);
            //TestIllegalNotVsProtectable();
            //TestLegalUseOfWrapper();
            //TestBasicVaultOfNullableNotUnmanagedValueType();
            //TestNoBasicVaultForStringBuilder();
            **/
        }

        public static void TestUriIsVs()
        {
            
            BasicVault<string> bvs = new BasicVault<string>("Hi mom", TimeSpan.FromSeconds(2));
            //BasicVault<StringBuilder> bvsb = new BasicVault<StringBuilder>(new StringBuilder(), TimeSpan.FromSeconds(2));
            using (var lck = bvs.SpinLock())
            {
                Console.WriteLine(lck.Value);
            }
            

        }

        public static void TestBasicVaultOfNullableNotUnmanagedValueType()
        {
            BasicVault<NotUnmanagedButVaultSafeValueType> bv = new BasicVault<NotUnmanagedButVaultSafeValueType>(new NotUnmanagedButVaultSafeValueType("Chris"));
            using (var lck = bv.Lock())
            {
                Console.WriteLine(lck.Value.Name);
            }

            var thisTimeWithJohn = CreateBasicVault((string s) => new NotUnmanagedButVaultSafeValueType(s));
            using (var lck = thisTimeWithJohn.Lock())
            {
                Console.WriteLine(lck.Value.Name );
            }

            //BUG 49 fix verification
            BasicVault<NotUnmanagedButVaultSafeValueType?> nullableVb = new BasicVault<NotUnmanagedButVaultSafeValueType?>(null, TimeSpan.FromSeconds(2));
            using (var lck = nullableVb.Lock())
            {
                Console.WriteLine(lck.Value?.Name ?? "NULL");
                lck.Value = new NotUnmanagedButVaultSafeValueType("Atticus");
            }

            //Bug 49 fix verification as applied to Type Argument
            BasicVault<NotUnmanagedButVaultSafeValueType?> nullableVb2 = CreateBasicVault((string s) =>
                (NotUnmanagedButVaultSafeValueType?) new NotUnmanagedButVaultSafeValueType(s));
            using (var lck = nullableVb2.SpinLock())
            {
                Console.WriteLine(lck.Value?.ToString() ?? "NULL");
                lck.Value = null;
                Console.WriteLine(lck.Value?.ToString() ?? "NULL");
            }
        }

        public static BasicVault<T> CreateBasicVault<[VaultSafeTypeParam] T>([NotNull] Func<string, T> ctor)
        {
            if (ctor == null) throw new ArgumentNullException(nameof(ctor));
            return new BasicVault<T>(ctor("John"), TimeSpan.FromSeconds(2));
        }

        static void TestNoBasicVaultForStringBuilder()
        {
            BasicVault<string> bv = new BasicVault<string>("Hi mom", TimeSpan.FromSeconds(2));
            //BasicVault<StringBuilder> bv = new BasicVault<StringBuilder>(new StringBuilder("Hi mom"), TimeSpan.FromSeconds(2));
            using var l = bv.SpinLock();
            Console.WriteLine(l.Value.ToString());
        }

        static void TestIllegalNotVsProtectable()
        {
            StringBuilder [] sb = { new StringBuilder("Hi"), new StringBuilder("Mon"), };
            DateTime[] datesAndTimes = GenDateTime(10);

            ImmutableArray<StringBuilder> immutSb = sb.ToImmutableArray();
            ImmutableArray<DateTime> immut = datesAndTimes.ToImmutableArray();
            var wrapper = VsArrayWrapper<DateTime>.CreateArrayWrapper(datesAndTimes);
            // var wrapper2 = VsArrayWrapper<StringBuilder>.CreateArrayWrapper(sb);

            //NOT OK -- is "considered" vault safe but isn't allowed as a protected resource
            //BasicVault<VsArrayWrapper<DateTime>> v = new BasicVault<VsArrayWrapper<DateTime>>(wrapper);

            //Ok -- ImmutableArray<DateTime> is vault safe
            //BasicVault<ImmutableArray<DateTime>> v = new BasicVault<ImmutableArray<DateTime>>(immut);

            //ok resource is not vault safe but mutresv doesnt require it
            MutableResourceVault<ImmutableArray<StringBuilder>> b =
                MutableResourceVault<ImmutableArray<StringBuilder>>.CreateAtomicMutableResourceVault(
                    () => immutSb.ToImmutableArray(), TimeSpan.FromSeconds(2));
            
            //fixed it -- included all invocation returns not just object creation expression
            //not ok -- is considered vault-safe, but cannot be a protected resource (not even a non-vs one)
           // MutableResourceVault<VsArrayWrapper<DateTime>> mrv = MutableResourceVault<VsArrayWrapper<DateTime>>.CreateMutableResourceVault(() =>
            //   wrapper, TimeSpan.FromSeconds(2));

     

            static DateTime[] GenDateTime(int count)
            {
                DateTime[] arr = new DateTime[count];
                int made = 0;
                while (made < count)
                {
                    long smallTicks = (long) RGen.Next();
                    smallTicks <<= 30;
                    arr[made++] = DateTime.MinValue + TimeSpan.FromTicks(smallTicks);
                }
                return arr;
            }
        }

        static void TestLegalUseOfWrapper()
        {
            MutableResourceVault<List<string>> mrvOfListOfString =
                MutableResourceVault<List<string>>.CreateAtomicMutableResourceVault(
                    () => new List<string> { "Ramsey", "Cersei", "Joffrey" }, TimeSpan.FromSeconds(2));

            using var lockedStrList = mrvOfListOfString.Lock();
            VsEnumerableWrapper<string> ancillary = VsEnumerableWrapper<string>.FromIEnumerable(AddUs);
            //proper use is to wrap something like IEnumerable that has a vault safe type argument into an effectively vault-safe
            //construct that can be used in delegate.  Because it is a wrapper around mutable state, it is not really vault safe, 
            //but it can be considered so for use in these delegates.
            lockedStrList.ExecuteAction((ref List<string> l, in VsEnumerableWrapper<string> a) =>
            {
                l.AddRange(a);
            }, ancillary);

            lockedStrList.ExecuteAction((ref List<string> l) =>
            {
                foreach (var s in l)
                {
                    Console.WriteLine("Villain: {0}", s);
                }
            });
        }

        static IEnumerable<string> AddUs
        {
            get
            {
                yield return "Littlefinger";
                yield return "Euron";
            }
        }

        static readonly Random RGen = new Random();
    }

    /// <summary>
    /// bug fix 48 -- event args can be a base class of a vault-safe type
    /// even though it itself isn't
    /// </summary>
    [VaultSafe]
    sealed class VsEventArgs : EventArgs
    {

    }

    //bug fix 48
    //The true parameter on the attribute will make analyzer consider it vault-safe even though it isn't
    [VaultSafe(10-1 == 8+1)]
    sealed class BadIdeaButWillWork : BadEventArgs
    {

    }

    //bug 48 fix -- this should not work -- bad event args cannot be base class of non-vault-safe type
    //[VaultSafe]
    //sealed class ShouldNotWork : BadEventArgs
    //{

    //}

    //This is not a valid base class for event args even though eventargs (empty) is ok
    class BadEventArgs : EventArgs
    {
        public StringBuilder Sb { get; set; } = new StringBuilder();
    }

    //post bug fix 48 -- base exception class still ok -- but not necessarily derived types.
    [VaultSafe]
    sealed class ExceptionNoAdd : Exception
    {

    }

    //Fixed bug 48 -- no longer deemed vault safe
    ///// <summary>
    ///// problematical -- base class is not vault-safe
    ///// </summary>
    //[VaultSafe]
    //sealed class MyException : MutableException
    //{

    //}


    //Won't compile -- not vault safe
    //[VaultSafe]
    //sealed class MutableException : Exception
    //{
    //    public override string Message => _sb.ToString();

    //    [NotNull] private readonly StringBuilder _sb = new StringBuilder();
    //}

    class MutableException : Exception
    {
        public StringBuilder Help => _sb;
        public sealed override string Message => _sb.ToString();

        [NotNull] private readonly StringBuilder _sb = new StringBuilder();
    }

    [VaultSafe]
    public readonly struct NotUnmanagedButVaultSafeValueType
    {
        [NotNull] public string Name => _name ?? string.Empty;
        public DateTime TimeStamp { get; }
        public Guid Id { get; }

        public NotUnmanagedButVaultSafeValueType(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            TimeStamp = DateTime.Now;
            Id = Guid.NewGuid();
        }
        
        private readonly string _name;
    }
}
