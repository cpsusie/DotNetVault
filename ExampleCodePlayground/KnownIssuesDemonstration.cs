using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace ExampleCodePlayground
{
    public sealed class KnownIssuesDemonstration
    {
        //BUG FIX 61 ... lck.Dispose used to cause potential race condition
        //bug fix 61 ... now lck.Dispose causes compiler error thanks to NotDirectlyInvocableAttribute
        //bug fix 61 ... which now annotates locked resource object's dispose method.  They must be disposed
        //bug fix 61 ... via using, must be declared inline, must not be disposed manually.
        public static void DemonstrateDoubleDispose()
        {
            using (var bv = new BasicVault<DateTime>(DateTime.Now, TimeSpan.FromMilliseconds(250)))
            {
                {
                    using var lck = bv.Lock();
                    Console.WriteLine(lck.Value.ToString("O"));
                    lck.Value = lck.Value + TimeSpan.FromDays(25);
                    //BUG 61 -- potential race condition / Known Flaw #1
                    //BUG 61 FIX -- POTENTIAL RACE CONDITION -- DO NOT DISPOSE MANUALLY IN THIS MANNER!
                    //lck.Dispose();
                    AnotherDemonstrationOfBug61Fix(in lck);
                    Console.WriteLine(lck.Value.ToString("O"));
                }
                //don't want this line but it will not trigger the NotDirectlyInvocable error because basic vault ctor not annotated with NotDirectlyInvocableAttribute
                // bv.Dispose();
            }
        }

        public static void AnotherDemonstrationOfBug61Fix(in LockedVaultObject<BasicVault<DateTime>, DateTime> lvo)
        {
            //add another 25 days
            lvo.Value = lvo.Value + TimeSpan.FromDays(25);
            //UNCOMMENTING FOLLOWING LINE WILL CAUSE COMPILER ERROR -- BUG FIX 61
            //lvo.Dispose();
        }

        public static void DemonstrateBadExtensionMethod()
        {
            using (var mrv =
                MutableResourceVault<StringBuilder>.CreateMutableResourceVault(() => new StringBuilder("AWESOME"),
                    TimeSpan.FromMilliseconds(250)))
            {
                int lengthOfTheStringBuilder;
                {
                    using var lck = mrv.Lock();
                    //BUG 62 FIX -- following line will not compile now given fix
                    //lengthOfTheStringBuilder = lck.ExecuteQuery((in StringBuilder sb) => sb.GetStringBuilderLength());
                    
                    //get it this way now that the way we were demonstrating no longer compiles post fix bug 62
                    lengthOfTheStringBuilder = lck.ExecuteQuery((in StringBuilder sb) => sb.Length);
                }

                Console.WriteLine($"The length of the string builder is: {lengthOfTheStringBuilder.ToString()}.");
                //BUG# 62 Known Flaw# 2 BAD -- Potential Race Condition on Protected Resource
                //BUG BAD -- if this were multithreaded defeats vault causing race condition -- I altered protected resource outside vault
                Flaw2StringBuilderExtensions.LastEvaluatedStringBuilder.AppendLine("\nHi mom!");

                //Used to show how Flaw2StringBuilderExtension.Last...AppendL above affected
                //the protected resource

                //Show the protected resource has been effected without accessing lock:
                {
                    using var lck = mrv.Lock();
                    string printMe = lck.ExecuteQuery((in StringBuilder sb) => sb.ToString());
                    Console.WriteLine($"After bug fix 62, no Hi mom! here!!!: [{printMe}].");
                }

                //The following wouldn't compile even before!
                //N.B. the following code (static function rather than extension function syntax) is caught by the analyzer:
                //{
                //    using var lck = mrv.Lock();
                //    lengthOfTheStringBuilder = lck.ExecuteQuery((in StringBuilder sb) =>
                //        Flaw2StringBuilderExtensions.GetStringBuilderLength(sb));
                //}
                //Console.WriteLine(lengthOfTheStringBuilder);
            }
        }

        public static void DemonstrateBadArrayBecauseTypeItselfInherentlyLeaksOwnState()
        {
            using (var mrv =
                MutableResourceVault<BadArray>.CreateMutableResourceVault(() => new BadArray(),
                    TimeSpan.FromMilliseconds(250)))
            {
                {
                    using var lck = mrv.Lock();
                    lck.ExecuteAction((ref BadArray ba) =>
                    {
                        DateTime now = DateTime.Now;
                        ba.Add(now);
                        ba.Add(now + TimeSpan.FromDays(1));
                        ba.Add(now + TimeSpan.FromDays(2));
                    });
                }
                //N.B. FLAW 3 This type inherently and shamelessly leaks shared mutable state.  The following
                //would affect the protected resource without obtaining lock and cause potential race condition!
                Console.WriteLine(
                    $"The last date time in the last updated bad array is: [{BadArray.LastBadArrayCreatedOrUpdated?.Last().ToString("O") ?? "EMPTY"}]");
                //now we'll go ahead and mutate it without a lock
                BadArray.LastBadArrayCreatedOrUpdated?.Add(DateTime.Now + TimeSpan.FromDays(4));
                //demonstrate that the protected resource has been mutated without lock:
                string printedLastDt;
                {
                    using var lck = mrv.Lock();
                    printedLastDt = lck.ExecuteQuery((in BadArray ba) => ba.Last().ToString("O"));
                }
                Console.WriteLine($"The last element has changed to: [{printedLastDt}].");
            }
        }
    }

    public static class Flaw2StringBuilderExtensions
    {
        public static StringBuilder LastEvaluatedStringBuilder { get; set; } = new StringBuilder();

        //BAD -- LEAKS SHARED MUTABLE STATE 
        public static int GetStringBuilderLength(this StringBuilder sb)
        {
            LastEvaluatedStringBuilder = sb ?? throw new ArgumentNullException(nameof(sb));
            return sb.Length;
        }
    }

    public sealed class BadArray : IEnumerable<DateTime>
    {
        [CanBeNull] public static BadArray LastBadArrayCreatedOrUpdated { get; set; }
        public IEnumerator<DateTime> GetEnumerator() => _list.GetEnumerator();
        public int Count => _list.Count;
        public BadArray() => _list = new List<DateTime>();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _list).GetEnumerator();
        public void Add(DateTime item)
        {
            _list.Add(item);
            LastBadArrayCreatedOrUpdated = this;
        }
        public DateTime this[int index]
        {
            get => _list[index];
            set
            {
                _list[index] = value;
                LastBadArrayCreatedOrUpdated = this;
            }
        }
        [NotNull] private readonly List<DateTime> _list;
    }

}

