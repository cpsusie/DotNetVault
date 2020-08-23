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

    static class ExtensionMethodPbvTest
    {
        public static void Foo()
        {
            using var chris = NoCopyAttributeWithBadAssignment.CreateValue("Chris");
            Console.WriteLine(chris.BadIsChris());
            Console.WriteLine(chris.GoodIsChris()); 
        }

    }

    static class ExtensionMethods
    {
        public static bool BadIsChris(this NoCopyAttributeWithBadAssignment ncba) => string.Equals(ncba.Name, "Chris");
        public static bool GoodIsChris(this in NoCopyAttributeWithBadAssignment ncba) => string.Equals(ncba.Name, "Chris");

    }
}
