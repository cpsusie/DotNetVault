using System;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// When this attribute annotates a method, the method may not be called directly.
    /// Its original intended use-case was for LockedResourceObject's Dispose().  Such objects are
    /// annotated with the <see cref="UsingMandatoryAttribute"/> meaning that they must be guarded by a
    /// using construct.  The problem arose that if one called Dispose manually, one could then still access the protected
    /// resource while the variable remained in scope even though it was no longer protected.  This attribute
    /// solves the problem by prohibiting direct invocation of the method -- it may only be called indirectly via using.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NoDirectInvokeAttribute : Attribute
    {
        /// <inheritdoc />
        public override string ToString() => $"[{typeof(NoDirectInvokeAttribute).Name}] -- " +
                                             $"A method annotated with this attribute may not be invoked directly.";
    }
}
