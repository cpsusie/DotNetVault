using System;
using System.Diagnostics;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.LockedResources
{
    /// <summary>
    /// The locked resource type returned by <see cref="BasicMonitorVault{T}"/>.  Since <typeparamref name="T"/> is
    /// and must be Vault-Safe, it is far less restrictive than <see cref="LockedVaultMutableResource{TVault,TResource}"/>
    ///
    /// You can access the value via the <see cref="Value"/> property.  Note that if you store <see cref="Value"/> in a local,
    /// you will be accessing a COPY of the resource.  If you wish to mutate the protected resource, must be accomplished via the
    /// <see cref="Value"/> property not a local or stored copy of it.
    /// </summary>
    /// <typeparam name="TVault">The vault type</typeparam>
    /// <typeparam name="T">the resource type</typeparam>
    [NoCopy]
    [RefStruct]
    public readonly ref struct LockedMonVaultObject<TVault, [VaultSafeTypeParam] T> where TVault : MonitorVault<T>, IBasicVault<T>
    {
        internal static LockedMonVaultObject<TVault, T> CreateLockedResource([NotNull] TVault vault,
            [NotNull] Vault<T>.Box box)
        {
            Func<MonitorVault<T>, Vault<T>.Box, Vault<T>.Box> releaseFunc = MonitorVault<T>.ReleaseResourceMethod;
            return new LockedMonVaultObject<TVault, T>(vault, box, releaseFunc);
        }

        /// <summary>
        /// Access the protected value
        /// </summary>
        [BasicVaultProtectedResource]
        public ref T Value => ref _box.Value;

        private LockedMonVaultObject([NotNull] TVault v, [NotNull] Vault<T>.Box b,
            [NotNull] Func<MonitorVault<T>, Vault<T>.Box, Vault<T>.Box> disposeMethod)
        {
            Debug.Assert(v != null && b != null && disposeMethod != null);
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
                Vault<T>.Box ok = _disposeMethod(_vault, b);
                Debug.Assert(ok == null);
            }
        }


        private readonly Vault<T>.Box _box;
        private readonly TVault _vault;
        private readonly Func<MonitorVault<T>, Vault<T>.Box, Vault<T>.Box> _disposeMethod;
        private readonly DisposeFlag _flag;
    }
}
