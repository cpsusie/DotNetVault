using System;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    public abstract class VaultAcqBehaviorTest : TestBase<VaultFactoryFixture>
    {
        protected VaultAcqBehaviorTest([NotNull] ITestOutputHelper helper, [NotNull] VaultFactoryFixture fixture) : base(helper, fixture)
        {
        }
    }

    public abstract class TestBase<TFixture> : IClassFixture<TFixture> where TFixture : class
    {
        [NotNull] public ITestOutputHelper Helper { get; }
        [NotNull] public TFixture Fixture { get; }

        protected TestBase([NotNull] ITestOutputHelper helper, [NotNull] TFixture fixture)
        {
            Helper = helper ?? throw new ArgumentNullException(nameof(helper));
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }
    }
}
