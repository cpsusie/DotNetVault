using System;
using JetBrains.Annotations;

namespace DotNetVault.Vaults
{

    internal interface IMutableResourceVaultFactory<TDerivedMutableResourceVault, TResource> 
    {
        /// <summary>
        /// Create a vault
        /// </summary>
        /// <param name="mutableResourceCreator">the function that creates a new mutable resource that doesn't get shared
        /// anywhere else, just returned</param>
        /// <param name="defaultTimeout">the default timeout</param>
        /// <param name="basicCtor">a delegate that executes <typeparamref name="TDerivedMutableResourceVault"/>'s constructor
        /// and returns it.</param>
        /// <returns>a vault of type <typeparamref name="TDerivedMutableResourceVault"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="mutableResourceCreator"/> or <paramref name="basicCtor"/> was null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> not positve.</exception>
        TDerivedMutableResourceVault CreateMutableResourceVault([NotNull] Func<TResource> mutableResourceCreator,
            TimeSpan defaultTimeout, [NotNull] Func<TDerivedMutableResourceVault> basicCtor);
    }
}