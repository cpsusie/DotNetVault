using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace ExampleCodePlayground
{
    public sealed class KnownIssuesDemonstration
    {
        public static void DemonstrateDoubleDispose()
        {
            using (var bv = new BasicVault<DateTime>(DateTime.Now, TimeSpan.FromMilliseconds(250)))
            {
                {
                    using var lck = bv.Lock();
                    Console.WriteLine(lck.Value.ToString("O"));
                    lck.Value = lck.Value + TimeSpan.FromDays(25);
                    //BUG 61 -- potential race condition / Known Flaw #1
                    //POTENTIAL RACE CONDITION -- DO NOT DISPOSE MANUALLY IN THIS MANNER!
                    lck.Dispose();
                    Console.WriteLine(lck.Value.ToString("O"));
                }
            }
        }

        public static void DemonstrateBadExtensionMethod()
        {
            using (var mrv =
                MutableResourceVault<StringBuilder>.CreateMutableResourceVault(() => new StringBuilder(),
                    TimeSpan.FromMilliseconds(250)))
            {
                int lengthOfTheStringBuilder;
                {
                    using var lck = mrv.Lock();
                    lengthOfTheStringBuilder = lck.ExecuteQuery((in StringBuilder sb) => sb.GetStringBuilderLength());
                }

                Console.WriteLine($"The length of the string builder is: {lengthOfTheStringBuilder.ToString()}.");
                //BUG# 62 Known Flaw# 2 BAD -- Potential Race Condition on Protected Resource
                //BUG BAD -- if this were multithreaded defeats vault causing race condition -- I altered protected resource outside vault
                Flaw2StringBuilderExtensions.LastEvaluatedStringBuilder.AppendLine("Hi mom!");

                //Show the protected resource has been effected without accessing lock:
                {
                    using var lck = mrv.Lock();
                    string printMe = lck.ExecuteQuery((in StringBuilder sb) => sb.ToString());
                    Console.WriteLine($"BAD -- look, we mutated the protected resource: [{printMe}].");
                }

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
        public static StringBuilder LastEvaluatedStringBuilder { get; set; }

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

