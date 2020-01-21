
using System;
using DotNetVault.Attributes;
using DotNetVault.TestCaseHelpers;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public sealed class TestBaseClassWbVsIfSealed  : WouldBeVaultSafeIfSealed
    {
        public Guid Id { get; }
        public TestBaseClassWbVsIfSealed([NotNull] string name, int age) : base(name, age) =>
            Id = Guid.NewGuid();


        protected override WouldBeVaultSafeIfSealed WithNewName(string name) =>
            new TestBaseClassWbVsIfSealed(name ?? throw new ArgumentNullException(nameof(name)), Age);

        protected override WouldBeVaultSafeIfSealed WithNewAge(int age) => new TestBaseClassWbVsIfSealed(Name, age);
    }
}
