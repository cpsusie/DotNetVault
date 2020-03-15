using System;

namespace DotNetVault.Vaults
{
    internal interface IBox<T> : IDisposable
    {
        bool IsDisposed { get; }
        ref T Value { get; }
        ref readonly T RoValue { get; }
    }
}
