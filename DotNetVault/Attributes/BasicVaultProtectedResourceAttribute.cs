using System;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// Used to annotate the Protected Value property of a Locked resource object's Value
    /// property when that value property returns by reference.  <see cref="IsReadOnly"/> should be true
    /// if it returns by readonly reference, false if by mutable reference.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class BasicVaultProtectedResourceAttribute : Attribute
    {
        /// <summary>
        /// True if the public resource is read-only, false otherwise
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Default construct the attribute, the <see cref="IsReadOnly"/> property will be
        /// set to <see langword="true"/>.
        /// </summary>
        public BasicVaultProtectedResourceAttribute() : this(false) { }

        /// <summary>
        /// Construct the attribute with its <see cref="IsReadOnly"/> property
        /// being set to the value of <paramref name="isReadOnly"/>.
        /// </summary>
        /// <param name="isReadOnly">true if the attribute annotates a read-only property, false otherwise.</param>
        public BasicVaultProtectedResourceAttribute(bool isReadOnly)
            => IsReadOnly = isReadOnly;

        internal const string ShortenedName = "BasicVaultProtectedResource";
    }
}
