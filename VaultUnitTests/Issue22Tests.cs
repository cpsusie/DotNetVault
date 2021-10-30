using System;
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
    public class Issue22Tests  : NullableAcqTests
    {
        /// <inheritdoc />
        public Issue22Tests([NotNull] ITestOutputHelper helper,
            [NotNull] NullableVtVaultFactoryFixture fixture) : base(helper, fixture) {}

        [Fact]
        public void TestGetLockUnsetNullableVt()
        {
            using (var vault = Fixture.CreateUnsetNullableVtVault())
            {
                {
                    using var lck = vault.Lock();
                    Helper.WriteLine($"Value now: {lck.Value?.ToString() ?? "NULL"}.");
                }
            }
        }

        [Fact]
        public void TestGetLockPresetNullableVt()
        {
            const ulong targetVal = 24;
            using (var vault = Fixture.CreateUnsetNullableVtVault())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(250), targetVal);
                {
                    using var lck = vault.Lock();
                    Assert.True(lck.Value == targetVal);
                }
            }
        }

        [Fact]
        public void TestGetLockConstructedNullableVt()
        {
            const ulong targetVal = 66;
            using (var vault = Fixture.CreateSetNullVtVault(targetVal))
            {
                using var lck = vault.Lock();
                Assert.True(lck.Value == targetVal);
            }
        }

        [Fact]
        public void TestGetLockUnsetVt()
        {
            using (var vault = Fixture.CreateUnsetVtVault())
            {
                {
                    using var lck = vault.Lock();
                    Helper.WriteLine($"Value now: {lck.Value.ToString()}.");
                }
            }
        }

        [Fact]
        public void TestGetLockPresetVt()
        {
            const ulong targetVal = 24;
            using (var vault = Fixture.CreateUnsetVtVault())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(250), targetVal);
                {
                    using var lck = vault.Lock();
                    Assert.True(lck.Value == targetVal);
                }
            }
        }

        [Fact]
        public void TestGetLockConstructedVt()
        {
            const ulong targetVal = 66;
            using (var vault = Fixture.CreateSetVtVault(targetVal))
            {
                using var lck = vault.Lock();
                Assert.True(lck.Value == targetVal);
            }
        }

        [Fact]
        public void TestGetLockUnsetRt()
        {
            using (var vault = Fixture.CreateUnsetVault())
            {
                {
                    using var lck = vault.Lock();
                    Helper.WriteLine($"Value now: {lck.Value ?? "NULL"}.");
                }
            }
        }

        [Fact]
        public void TestGetLockPreset()
        {
            const string targetVal = "FooBar";
            using (var vault = Fixture.CreateUnsetVault())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(250), targetVal);
                {
                    using var lck = vault.Lock();
                    Assert.True(lck.Value == targetVal);
                }
            }
        }

        [Fact]
        public void TestGetLockConstructedNullRt()
        {
            const string targetVal = null;
            using (var vault = Fixture.CreateSetVault(targetVal))
            {
                using var lck = vault.Lock();
                Assert.True(lck.Value == targetVal);
            }
        }

        [Fact]
        public void TestGetLockConstructedNotNullRtVault()
        {
            const string targetVal = "frobnication";
            using (var vault = Fixture.CreateSetVault(targetVal))
            {
                using var lck = vault.Lock();
                Assert.True(lck.Value == targetVal);
            }
        }

    }
}
