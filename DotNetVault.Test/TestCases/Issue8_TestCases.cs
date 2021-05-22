using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.Test.TestCases
{
    public enum TestCaseMode
    {
        NotWorking = 0,
        Working
    }

    [VaultSafe]
    public sealed class Issue8TestCase
    {
        [NotNull] public string FirstName { get;  }

        [NotNull] public string LastName { get; }

        public TestCaseMode? Mode { get; }

        public Issue8TestCase([NotNull] string firstName, string lastName)
        {
            FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
            LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
            Mode = TestCaseMode.NotWorking;
        }
    }
}
