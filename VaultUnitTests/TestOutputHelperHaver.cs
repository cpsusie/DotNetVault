using System;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    public abstract class TestOutputHelperHaver
    {
        [NotNull] public ITestOutputHelper TestOutputHelper { get; }

        protected TestOutputHelperHaver([NotNull] ITestOutputHelper helper)
        {
            if (helper == null) throw new ArgumentNullException(nameof(helper));
            TestOutputHelper = helper;
        }
    }
}