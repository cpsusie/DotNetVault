using System;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    public abstract class OutputHelperAndFixtureHavingTests<T> : TestOutputHelperHaver, IClassFixture<T> where T : class
    {
        [NotNull] public T Fixture { get; }

        protected OutputHelperAndFixtureHavingTests([NotNull] ITestOutputHelper helper, [NotNull] T fixture)
            : base(helper) => Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

}
