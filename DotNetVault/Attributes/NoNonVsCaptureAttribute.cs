using System;
using DotNetVault.CustomVaultExamples.CustomLockedResources;
using DotNetVault.LockedResources;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// This attribute is used to annotate the delegates used by locked mutable resource objects.
    /// <see cref="LockedVaultMutableResource{TVault,TResource}"/>  <seealso cref="LockedStringBuilder"/>.
    ///
    /// This attribute is designed to prevent any non-vault-safe state in the protected resource from leaking outside the
    /// lock/vault or to prevent any such external state from becoming entangled with the protected resource.  Enforcement
    /// of the rules this attribute applies is accomplished via static analysis.  The following are the summary of the rules:
    ///     1. No non-vault-safe items may be referenced in the delegate (except for the protected resource), whether those non-vault-safe
    ///        objects are static fields or properties or are captured directly or indirectly (if "this" is not vault safe type, not even this
    ///        may be indirectly captured or referenced during the operation).  You may create a NEW protected resource inside the delegate, may
    ///        must assign it in the same statement to the protected resource. 
    ///     2. The protected resource may not be passed as a parameter to method outside the delegate
    ///     3. If the protected resource happens to be a vault-safe value (which should not normally be protected using the <see cref="LockedVaultMutableResource{TVault,TResource}"/>
    ///        or custom-version like <seealso cref="LockedStringBuilder"/>) it may not be passed by reference to any method inside the delegate.
    /// </summary>
    [AttributeUsage(AttributeTargets.Delegate)]
    public sealed class NoNonVsCaptureAttribute : Attribute
    {
    }
}
