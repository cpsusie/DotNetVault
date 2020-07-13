using System;

namespace VaultUnitTests
{
    public interface IDualOperationResult : IEquatable<IDualOperationResult>
    {
        bool ResultsMatch { get; }

        string ToString();
    }
}