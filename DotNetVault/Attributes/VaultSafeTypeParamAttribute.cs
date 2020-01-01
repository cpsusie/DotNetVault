using System;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// This attribute annotates that a type parameter is constrained to arguments that are considered
    /// VaultSafe.  When a type is supplied to a generic type, method or delegate, and the parameter
    /// has corresponding to the argument is annotated with this attribute, static analysis will validate
    /// the vault-safety of the type argument.  Note that the analysis will be performed not only on a direct
    /// instantiation of the type with this annotation but also any derived types or types that implement an interface
    /// with a generic type parameter constrained by this type.
    /// </summary>
    [AttributeUsage(AttributeTargets.GenericParameter)]
    public sealed class VaultSafeTypeParamAttribute : Attribute
    {

    }
}