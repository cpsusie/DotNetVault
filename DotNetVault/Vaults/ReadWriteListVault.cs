using System;
using DotNetVault.Attributes;
using DotNetVault.RefReturningCollections;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// Base class for vaults that protect RefReturning Lists <see cref="ByRefList{T}"/>
    /// </summary>
    /// <typeparam name="TItem">The vault safe item type that is held in the list.</typeparam>
    /// <typeparam name="TList">The ref returning list of <typeparamref name="TItem"/>, which is the resource protected by this vault.</typeparam>
    public abstract class ReadWriteListVault<[VaultSafeTypeParam] TItem, TList> : ReadWriteVault<TList> where TList : ByRefList<TItem>
    {

        /// <summary>
        /// if timespan is not supplied to CTOR, this time period will be used.
        /// </summary>
        public static TimeSpan FallbackTimeout => TimeSpan.FromMilliseconds(250);
        private protected ReadWriteListVault(TList protectMe, TimeSpan defaultTimeout) : base(defaultTimeout) =>
            Init(protectMe ?? throw new ArgumentNullException(nameof(protectMe))); 


    }
}
