using System;
using DotNetVault.Attributes;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace VaultUnitTests
{
    public class VaultFactoryFixture
    {
        public virtual BasicVault<T> CreateBasicVault<[VaultSafeTypeParam] T>() =>
            BasicVaultFactorySource<T>.FactoryInstance(default, TimeSpan.FromMilliseconds(250));

        public virtual BasicMonitorVault<T> CreateBasicMonitorVault<[VaultSafeTypeParam] T>() =>
            BasicMonVaultFactorySource<T>.FactoryInstance(default, TimeSpan.FromMilliseconds(250));

        public virtual BasicReadWriteVault<T> CreateBasicReadWriteVault<[VaultSafeTypeParam] T>(TimeSpan? ts) =>
            new BasicReadWriteVault<T>(ts.HasValue && ts > TimeSpan.Zero ? ts.Value : TimeSpan.FromMilliseconds(250));

        public MutableResourceVault<T> CreateMrv<T>([NotNull] Func<T> ctor) =>
            MutableResourceVaultFactorySource<T>.FactoryInstance(ctor, TimeSpan.FromMilliseconds(250));
    }
    [NotNull]
    public delegate BasicMonitorVault<T> BasicMonitorVaultFactory<[VaultSafeTypeParam] T>(T initialValue, TimeSpan defaultTimeout);
    [NotNull]
    public delegate BasicVault<T> BasicVaultFactory<[VaultSafeTypeParam] T>(T initialValue, TimeSpan defaultTimeout);
    [NotNull]
    public delegate MutableResourceVault<T> MutableResourceVaultFactory<T>([NotNull] Func<T> valueCreator,
        TimeSpan defaultTimeout);
    
    internal static class BasicVaultFactorySource<[VaultSafeTypeParam] TVs>
    {
        [NotNull] public static BasicVaultFactory<TVs> FactoryInstance => TheBasicVaultFactory;

        public static bool SupplyAlternateFactory([NotNull] BasicVaultFactory<TVs> alternate) =>
            TheBasicVaultFactory.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        [NotNull] private static BasicVault<TVs> DefaultBvFactory(TVs initialValue, TimeSpan ts) => BasicVault<TVs>.CreateAtomicBasicVault(initialValue, ts);
        [NotNull] private static readonly LocklessWriteOnce<BasicVaultFactory<TVs>> TheBasicVaultFactory =
            new LocklessWriteOnce<BasicVaultFactory<TVs>>(() => DefaultBvFactory);
    }

    internal static class BasicMonVaultFactorySource<[VaultSafeTypeParam] TVs>
    {
        [NotNull] public static BasicMonitorVaultFactory<TVs> FactoryInstance => TheBasicVaultFactory;

        public static bool SupplyAlternateFactory([NotNull] BasicMonitorVaultFactory<TVs> alternate) =>
            TheBasicVaultFactory.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        [NotNull] private static BasicMonitorVault<TVs> DefaultBvFactory(TVs initialValue, TimeSpan ts) => new BasicMonitorVault<TVs>(initialValue, ts);
        [NotNull]
        private static readonly LocklessWriteOnce<BasicMonitorVaultFactory<TVs>> TheBasicVaultFactory =
            new LocklessWriteOnce<BasicMonitorVaultFactory<TVs>>(() => DefaultBvFactory);
    }

    internal static class MutableResourceVaultFactorySource<T>
    {
        [NotNull] public static MutableResourceVaultFactory<T> FactoryInstance => TheMrvFactory;
        public static bool SupplyAlternateFactory([NotNull] MutableResourceVaultFactory<T> alternate) =>
            TheMrvFactory.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        [NotNull] private static MutableResourceVault<T> DefaultMvFactory([NotNull] Func<T> initialValueCtor, TimeSpan defaultTimeout) =>
            MutableResourceVault<T>.CreateAtomicMutableResourceVault(initialValueCtor ?? throw new ArgumentNullException(nameof(initialValueCtor)), defaultTimeout);
        [NotNull] private static readonly LocklessWriteOnce<MutableResourceVaultFactory<T>> TheMrvFactory =
            new LocklessWriteOnce<MutableResourceVaultFactory<T>>(() => DefaultMvFactory);
    }
}
