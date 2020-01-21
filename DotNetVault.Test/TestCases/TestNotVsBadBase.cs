using System;
using DotNetVault.Attributes;
using DotNetVault.TestCaseHelpers;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    class TestNotVsBadBase : NotVaultSafeEvenIfSealed
    {
        public Guid Id { get; }

        public TestNotVsBadBase([NotNull] string name, int age) : 
            base(name, age) => Id = Guid.NewGuid();

    }
}
