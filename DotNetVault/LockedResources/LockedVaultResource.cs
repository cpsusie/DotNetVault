using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.LockedResources
{
    /// <summary>
    /// The locked resource type returned by <see cref="BasicVault{T}"/>.  Since <typeparamref name="T"/> is
    /// and must be Vault-Safe, it is far less restrictive than <see cref="LockedVaultMutableResource{TVault,TResource}"/>
    ///
    /// You can access the value via the <see cref="Value"/> property.  Note that if you store <see cref="Value"/> in a local,
    /// you will be accessing a COPY of the resource.  If you wish to mutate the protected resource, must be accomplished via the
    /// <see cref="Value"/> property not a local or stored copy of it.
    /// </summary>
    /// <typeparam name="TVault">The vault type</typeparam>
    /// <typeparam name="T">the resource type</typeparam>
    public readonly ref struct LockedVaultObject<TVault, [VaultSafeTypeParam] T> where TVault : Vault<T>  
    {
        internal static LockedVaultObject<TVault, T> CreateLockedResource([NotNull] TVault vault,
            [NotNull] Vault<T>.Box box)
        {
            Func<Vault<T>, Vault<T>.Box, Vault<T>.Box> releaseFunc = Vault<T>.ReleaseResourceMethod;
            return new LockedVaultObject<TVault, T>(vault, box, releaseFunc);
        }

        /// <summary>
        /// Access the protected value
        /// </summary>
        [BasicVaultProtectedResource]
        public ref T Value => ref _box.Value;
        
        private LockedVaultObject([NotNull] TVault v, [NotNull] Vault<T>.Box b,
            [NotNull] Func<Vault<T>, Vault<T>.Box, Vault<T>.Box> disposeMethod)
        {
            Debug.Assert(v != null && b != null && disposeMethod != null);
            _vault = v;
            _box = b;
            _disposeMethod = disposeMethod;
            _flag = new DisposeFlag();
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
                var temp = _disposeMethod(_vault, b);
                Debug.Assert(temp == null);
            }
        }

        private readonly DisposeFlag _flag;
        private readonly Func<Vault<T>, Vault<T>.Box, Vault<T>.Box> _disposeMethod;
        private readonly TVault _vault;
        private readonly Vault<T>.Box _box;
    }

    internal sealed class DisposeFlag
    {

        public bool IsSet => _isSet != NotSet;

        public bool TrySet()
        {
            const int wantToBe = Set;
            const int needToBeNow = NotSet;
            return Interlocked.CompareExchange(ref _isSet, wantToBe, needToBeNow) == needToBeNow;
        }

        public override string ToString() => $"DisposeFlag State: [{(IsSet ? "SET" : "CLEAR")}]";
        
        private volatile int _isSet = NotSet;
        private const int Set = 1;
        private const int NotSet = 0;
    }
}