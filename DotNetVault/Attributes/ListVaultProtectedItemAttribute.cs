using System;
using DotNetVault.Vaults;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// Annotates items that are protected resource items returned by reference from a
    /// vault derived from a <see cref="ReadWriteListVault{TItem,TList}"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.ReturnValue)]
    public sealed class ListVaultProtectedItemAttribute : Attribute
    {
        /// <summary>
        /// True if the public resource is read-only, false otherwise
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Create the attribute
        /// </summary>
        /// <param name="isReadOnly">True if it is read only, false otherwise.</param>
        public ListVaultProtectedItemAttribute(bool isReadOnly) => IsReadOnly = isReadOnly;


        /// <summary>
        /// Create the attribute ... not read only
        /// </summary>
        public ListVaultProtectedItemAttribute() : this(false) {}

        internal const string ShortenedName = "ListVaultProtectedItem";
    }
}