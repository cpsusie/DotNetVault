using System;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    public abstract class VaultAcqBehaviorTest : IClassFixture<VaultFactoryFixture>
    {
        [NotNull] public ITestOutputHelper Helper { get; }
        [NotNull] public VaultFactoryFixture Fixture { get; }

        protected VaultAcqBehaviorTest([NotNull] ITestOutputHelper helper, [NotNull] VaultFactoryFixture fixture)
        {
            Helper = helper ?? throw new ArgumentNullException(nameof(helper));
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

    }
}
