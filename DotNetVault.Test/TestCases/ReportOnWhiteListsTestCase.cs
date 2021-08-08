using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [ReportWhiteListLocations]
    public class ReportOnWhiteListsTestCase
    {
        public Guid Id { get; }
        public ReportOnWhiteListsTestCase() => Id = Guid.NewGuid();

    }
}
