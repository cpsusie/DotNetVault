using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public sealed class EventArgsOkTest : EventArgs
    {
        public EventArgsOkTest(DateTime? ts) => _ts = ts;

        public override string ToString() => $"Timestamp: [{_ts?.ToString("O") ?? "NONE"}].";
        

        private readonly DateTime? _ts;
    }
}
