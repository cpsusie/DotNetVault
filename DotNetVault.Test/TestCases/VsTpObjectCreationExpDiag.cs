using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    public sealed class VsTpObjectCreationExpDiag<[VaultSafeTypeParam] T>
    {
        public VsTpObjectCreationExpDiag([NotNull] T newV)
        {
            if (newV == null) throw new ArgumentNullException(nameof(newV));
            _val = newV;
        }

        public override string ToString() => _val.ToString();

        private readonly T _val;
    }

    public static class Tests
    {
        public static void TestProblem()
        {
            VsTpObjectCreationExpDiag<StringBuilder> bad =
                new VsTpObjectCreationExpDiag<StringBuilder>(new StringBuilder());
            Console.WriteLine(bad);
        }
    }

}
