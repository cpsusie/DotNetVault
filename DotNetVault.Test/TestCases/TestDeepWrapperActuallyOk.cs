using System;
using DotNetVault.Vaults;
using RwLck = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRwLockedResource;
using RoLck = DotNetVault.CustomVaultExamples.CustomLockedResources.StringBuilderRoLockedResource;
namespace DotNetVault.Test.TestCases
{
    public readonly ref struct FinalBadWrapper
    {
        public RwLck Lock => _rwLck;
        public DateTime Stamp => _ts;
        public string Text => _text ?? "NONE";

        internal FinalBadWrapper(in RwLck lck, string text)
        {
            _rwLck = lck;
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _ts = DateTime.Now;
        }

        private readonly DateTime _ts;
        private readonly string _text;
        private readonly RwLck _rwLck;
    }

    public readonly ref struct PenultimateBadWrapper
    {
        public FinalBadWrapper FinalBadWrapper => _fbr;
        public int Count => _count;

        internal PenultimateBadWrapper(in FinalBadWrapper fbr, int count)
        {
            _fbr = fbr;
            _count = count;
        }

        private readonly FinalBadWrapper _fbr;
        private readonly int _count;
    }

    public readonly ref struct AntepenultimateBadWrapper
    {
        public PenultimateBadWrapper BadWrapper { get; }
        public TimeSpan Period { get; }

        internal AntepenultimateBadWrapper(in PenultimateBadWrapper pubw, TimeSpan period)
        {
            Period = period;
            BadWrapper = pubw;
        }
    }

    public readonly ref struct FirstInTheChain
    {
        public static FirstInTheChain CreateDeeplyHiddenIllegalWrapper(in RwLck lck, TimeSpan span, string name,
            string anotherName)
        {
            FinalBadWrapper fbw = new FinalBadWrapper(in lck, anotherName);
            PenultimateBadWrapper pbw = new PenultimateBadWrapper(in fbw, 12);
            AntepenultimateBadWrapper apbw = new AntepenultimateBadWrapper(in pbw, span);
            return new FirstInTheChain(in apbw, name, DateTime.Now);
        }

        public AntepenultimateBadWrapper NextBadWrapper { get; }
        public string MyName { get; }
        public DateTime Stamp { get; }

        private FirstInTheChain(in AntepenultimateBadWrapper nextBadWrapper, string name, DateTime stamp)
        {
            NextBadWrapper = nextBadWrapper;
            MyName = name ?? "NO NAME";
            Stamp = stamp;
        }
    }

    public static class TestDeepIllegalWrapper
    {

        public static void FirstActuallyOkTest()
        {
            string joMama;
            {
                using RwLck lck = TheVault.Lock();
                lck.AppendLine("Hi mom!");
            }

            FirstInTheChain shouldNotBeAProblemHere = default;
            {
                RwLck decoy = default;
                using RoLck lck = TheVault.RoLock();
                shouldNotBeAProblemHere =
                    FirstInTheChain.CreateDeeplyHiddenIllegalWrapper(in decoy, TimeSpan.FromDays(1), "Fred", "George");
                joMama = lck.ToString();
            }

            Console.WriteLine(joMama);
            Console.WriteLine(shouldNotBeAProblemHere.NextBadWrapper.BadWrapper.FinalBadWrapper.Lock.ToString());
        }

        public static void SecondActuallyOkTest()
        {
            string joMama;
            {
                using RwLck lck = TheVault.Lock();
                lck.AppendLine("Hi mom!");
            }

            FirstInTheChain alsoShouldNotActuallyBeAProblemHere = default;


            using (RoLck anotherLck = TheVault.RoLock())
            {
                RwLck anotherDecoy = default;
                alsoShouldNotActuallyBeAProblemHere =
                    FirstInTheChain.CreateDeeplyHiddenIllegalWrapper(in anotherDecoy, TimeSpan.FromDays(1), "Fred", "George");
                joMama = anotherLck.ToString();
            }

            Console.WriteLine(joMama);
            Console.WriteLine(alsoShouldNotActuallyBeAProblemHere.NextBadWrapper.BadWrapper
                .FinalBadWrapper.Lock.ToString());
        }

        private static readonly ReadWriteStringBufferVault TheVault =
            new ReadWriteStringBufferVault(TimeSpan.FromMilliseconds(750));
    }
}
