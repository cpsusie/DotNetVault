using System;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// This attribute indicates that a class is VaultSafe and therefore
    /// easier to isolate.
    /// 
    /// Applying this to a type will trigger static analysis to ensure that
    /// it actually meets vault safety requirements.  If the type does not meet
    /// these requirements, it constitutes a compiler error.
    ///
    /// To learn more about vault safety, consult the usage guide, § 3.a
    ///
    /// If you wish the static analyzer to take the vault safety of a type on faith (blindly
    /// trust that the type is effectively vault-safe even though not statically provable to be
    /// vault-safe), use the parameterized constructor with the true value. 
    /// </summary>
    /// <remarks>Using the "OnFaith" parameter is not recommended for types over which you have
    /// control.  Some situations might warrant it (such as using your own sufficient
    /// synchronization mechanisms to guard the non-vault-safe portions), but special care must be taken:
    /// when changes are made to the type outside the ambit of your own protection, the lack of vault-safety
    /// will not be detected.
    ///
    /// If the class is OUTSIDE your control, you should instead add its namespace-qualified-name to the vaultsafewhitelist.txt
    /// file.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class VaultSafeAttribute : Attribute
    {
        /// <summary>
        /// True if the vault safety of the type decorated with this attribute
        /// should be considered vault-safe on faith, without performing
        /// static analysis to validate the vault safety.
        /// </summary>
        public bool OnFaith { get; }

        /// <summary>
        /// CTOR for the attribute
        /// </summary>
        /// <param name="onFaith">true if vault-safety should be taken
        /// on faith, false if verified by static analysis</param>
        public VaultSafeAttribute(bool onFaith) => OnFaith = onFaith;
        
        /// <summary>
        /// CTOR for the attribute.  Vault-Safety will be verified by
        /// static analysis.
        /// </summary>
        public VaultSafeAttribute() : this(false) { }
    }
}
