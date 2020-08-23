using System;
using System.Diagnostics;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.LockedResources
{
    /// <summary>
    /// This locked resource type is returned by <see cref="ReadWriteVault{T}"/>s whose generic parameter is vault-safe
    /// when a non-upgradable read-only lock is requested.
    ///
    /// You can read the value by reference and access non mutating methods via the <see cref="Value"/> property.
    /// May not be used to propagate changes.  Ref local aliasing is forbidden by the <see cref="BasicVaultProtectedResourceAttribute"/>
    /// </summary>
    /// <typeparam name="TVault">The vault type</typeparam>
    /// <typeparam name="T">The resource type (must be vault safe) </typeparam>
    [NoCopy]
    [RefStruct]
    public readonly ref struct ReadOnlyRwLockedResource<TVault, [VaultSafeTypeParam] T> where TVault : ReadWriteVault<T>
    {
        internal static ReadOnlyRwLockedResource<TVault, T> CreateReadOnlyLockedResource([NotNull] TVault v,
            [NotNull] Vault<T>.Box b)
        {
            Func<ReadWriteVault<T>, Vault<T>.Box, AcquisitionMode, Vault<T>.Box> releaseMethod =
                ReadWriteVault<T>.ReleaseResourceMethod;
            return new ReadOnlyRwLockedResource<TVault, T>(v, b, releaseMethod);
        }

        /// <summary>
        /// Access to the protected value by readonly reference
        /// </summary>
        [BasicVaultProtectedResource(true)]
        public ref readonly T Value => ref _box.RoValue;

        private ReadOnlyRwLockedResource([NotNull] TVault v, [NotNull] Vault<T>.Box b,
            [NotNull] Func<TVault, Vault<T>.Box, AcquisitionMode, Vault<T>.Box> disposeMethod)
        {
            Debug.Assert(b != null && v!= null && disposeMethod != null);
            _box = b;
            _disposeMethod = disposeMethod;
            _flag = new DisposeFlag();
            _vault = v;
        }

        /// <summary>
        /// release the lock and return the protected resource to vault for use by others
        /// </summary>
        [NoDirectInvoke]
        public void Dispose()
        {
            if (_flag?.TrySet() == true)
            {
                Vault<T>.Box b = _box;
                // ReSharper disable once RedundantAssignment DEBUG vs RELEASE
                var temp = _disposeMethod(_vault, b, Mode);
                Debug.Assert(temp==null);
            }
        }

        private readonly DisposeFlag _flag;
        private readonly Func<TVault, Vault<T>.Box, AcquisitionMode, Vault<T>.Box> _disposeMethod;
        private readonly Vault<T>.Box _box;
        private readonly TVault _vault;
        private const AcquisitionMode Mode = AcquisitionMode.ReadOnly;
    }
}
