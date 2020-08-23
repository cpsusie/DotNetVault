using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [NoCopy]
    public readonly ref struct NoCopyAttributeWithBadAssignment
    {
        [return: UsingMandatory]
        public static NoCopyAttributeWithBadAssignment CreateValue([NotNull] string s) =>
            new NoCopyAttributeWithBadAssignment(s);
        public string Name => _name ?? string.Empty;

        public void Dispose()
        {

        }
        private NoCopyAttributeWithBadAssignment(string n) => _name = n ?? throw new ArgumentNullException(nameof(n));
        private readonly string _name;
    }

    public ref struct NoCopyAttributeWrapper
    {
        public NoCopyAttributeWithBadAssignment WrapMe;
        public DateTime TimeStamp;

        public NoCopyAttributeWrapper(NoCopyAttributeWithBadAssignment val, DateTime ts) 
        {
            WrapMe = val;
            TimeStamp = ts;
        }
    }

    public static class TestMe
    {
        public static void Foo()
        {
            NoCopyAttributeWithBadAssignment ncba;
            { 
                using var x = NoCopyAttributeWithBadAssignment.CreateValue("Chris");     
                ncba = x;    

                DoStuffByConstReference(in x, DateTime.Now); 
                DoStuffByValue(x, DateTime.Now); 
                DoStuffByValue(stamp: DateTime.Now, val: x);
                DoStuffByConstReference(ts: DateTime.Now, bcba: in x);
                NoCopyAttributeWrapper wrapper = new NoCopyAttributeWrapper{TimeStamp = DateTime.Now, WrapMe = x};
            }
            Console.WriteLine(ncba.Name);
        }

        public static void Bar()
        {
            NoCopyAttributeWithBadAssignment ncba;
            using (var x = NoCopyAttributeWithBadAssignment.CreateValue("Chris"))   
            {
                ncba = x;
                DoStuffByConstReference(in x, DateTime.Now);
                DoStuffByValue(x, DateTime.Now);
                DoStuffByValue(stamp: DateTime.Now, val: x);
                DoStuffByConstReference(ts: DateTime.Now, bcba: in x);
                NoCopyAttributeWrapper wrapper = new NoCopyAttributeWrapper { TimeStamp = DateTime.Now, WrapMe = x }; 
                NoCopyAttributeWrapper wrapper2 = new NoCopyAttributeWrapper(x, DateTime.Now);
            }
            
            
            Console.WriteLine(ncba.Name);
        }

        public static void DoStuffByConstReference(in NoCopyAttributeWithBadAssignment bcba, DateTime ts)
        {
            Console.WriteLine("Val: [" + bcba.Name + "], Stamp: [" + ts.ToString("O") + "].");
        }
        public static void DoStuffByNonConstReference(ref NoCopyAttributeWithBadAssignment bcba, DateTime ts)
        {
            Console.WriteLine("Val: [" + bcba.Name + "], Stamp: [" + ts.ToString("O") + "].");
        }

        public static void DoStuffByOutRef(out NoCopyAttributeWithBadAssignment ncba, DateTime ts, in NoCopyAttributeWithBadAssignment bad)
        {
            ncba = bad;
            Console.WriteLine("Hi mom!");
        }

        public static void DoStuffByValue(NoCopyAttributeWithBadAssignment val, DateTime stamp)
        {
            Console.WriteLine("Val: [" + val.Name + "], Stamp: [" + stamp.ToString("O") + "].");
        }
    }
}
