using System;
using Xunit.Abstractions;

namespace VaultUnitTests.ClortonGame
{
    public interface IDisposableOutputHelper : ITestOutputHelper, IDisposable { }
}