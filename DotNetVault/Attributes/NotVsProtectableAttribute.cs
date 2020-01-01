using System;
using DotNetVault.LockedResources;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// This attribute designates that objects of this type are not allowed to be the protected
    /// resource of a Vault.  This is typically applied to convenience wrappers over collections that
    /// are allow by the vault safe attribute to be referenced in delegates such as <see cref="VaultQuery{TResource,TResult}"/>,
    /// <see cref="VaultQuery{TResource,TAncillary, TResult}"/>, <see cref="VaultAction{TResource}"/>,
    /// <see cref="VaultAction{TResource, TAncillary}"/>, <see cref="VaultMixedOperation{TResource,TResult}"/> and
    /// <see cref="VaultMixedOperation{TResource, TAncillary, TResult}"/>, but nevertheless are not intended or suitable
    /// to be actual protected resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class NotVsProtectableAttribute : Attribute
    {
    }
}
