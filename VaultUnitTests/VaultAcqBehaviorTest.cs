using System;
using DotNetVault.Exceptions;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using ResourceType = System.String;
using NullVtType = System.Nullable<System.UInt64>;
using VtType = System.UInt64;
namespace VaultUnitTests
{
    using VaultType = DotNetVault.Vaults.BasicMonitorVault<ResourceType>;
    using NullVtVault = DotNetVault.Vaults.BasicMonitorVault<NullVtType>;
    using VtVault = DotNetVault.Vaults.BasicMonitorVault<VtType>;
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

    public class NullableVtVaultFactoryFixture : VaultFactoryFixture
    {
        public TimeSpan VaultTimeout
        {
            get => _timeout;
            init => _timeout = value > TimeSpan.Zero
                ? value
                : throw new ArgumentNotPositiveException<TimeSpan>(nameof(value), value);
        }

        public NullVtVault CreateSetNullVtVault(NullVtType initial) => new(initial, _timeout);

        public NullVtVault CreateUnsetNullableVtVault() => new (_timeout);

        public VtVault CreateUnsetVtVault() => new(_timeout);

        public VtVault CreateSetVtVault(VtType value) => new(value, _timeout);

        public VaultType CreateUnsetVault() => new(_timeout);

        public VaultType CreateSetVault(ResourceType rt) => new(rt, _timeout);
        

        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(500);
    }

    public abstract class NullableAcqTests : TestBase<NullableVtVaultFactoryFixture>
    {
        /// <inheritdoc />
        protected NullableAcqTests([NotNull] ITestOutputHelper helper, 
            [NotNull] NullableVtVaultFactoryFixture fixture) : base(helper, fixture) { }
    }


}
