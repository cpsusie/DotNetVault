using System;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// This attribute will cause a diagnostic to be emitted containing the path
    /// of the whitelist file and the conditional generic whitelist file.
    /// </summary>
    /// <remarks>This attribute may be applied to class and struct declarations</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class ReportWhiteListLocationsAttribute : Attribute
    {
        internal static readonly string ShortenedName = "ReportWhiteListLocations";
    }
}
