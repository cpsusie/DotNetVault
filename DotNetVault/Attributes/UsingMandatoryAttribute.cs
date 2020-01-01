using System;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// This attribute is decorates the return value of invocation expressions.
    /// It mandates that the IMMEDIATE caller protected the value returned by a using statement
    /// or declaration, making it an error to fail to do so.
    /// </summary>
    /// <remarks>
    /// While a try...finally construct may suffice, these are bugprone and hard to do correctly.
    /// For example, to be sure the dispose is executed, it should be the only statement in the finally block.
    /// Putting anything else first might throw an exception, causing the dispose method not to be invoked and
    /// the probable result would be a silent deadlock. 
    /// </remarks>
    [AttributeUsage(AttributeTargets.ReturnValue)]
    public sealed class UsingMandatoryAttribute : Attribute
    {
        internal static readonly string ShortenedName = "UsingMandatory";
    }
}
