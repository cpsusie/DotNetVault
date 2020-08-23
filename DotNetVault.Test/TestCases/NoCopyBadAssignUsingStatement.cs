using System;
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
        public new string ToString() => Name;
        public void Dispose()
        {

        }
        private NoCopyAttributeWithBadAssignment(string n) => _name = n ?? throw new ArgumentNullException(nameof(n));
        private readonly string _name;
    }

    static class Foo
    {
        public static void Bar()
        {
            string name;
            NoCopyAttributeWithBadAssignment ncba; 
            using (var x  = NoCopyAttributeWithBadAssignment.CreateValue("Chris"))     
            {
                ncba = x;
                name = x.Name; 
                Console.WriteLine(ncba.Name);
                Console.WriteLine(x.Name);
                Console.WriteLine(x);
                PrintByConstReference(in x);
            }
            //using (var y = NoCopyAttributeWithBadAssignment.CreateValue("Chris"))
            //                ncba = x;
            

            Console.WriteLine(ncba.Name);
        }

        private static void PrintByConstReference(in NoCopyAttributeWithBadAssignment ncba)
            => Console.WriteLine(ncba.ToString());
    }
}
