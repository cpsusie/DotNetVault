using System;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    public sealed class NullableNotHeldAgainstMe
    {
        public bool HasTs => _ts != null;
        public NullableNotHeldAgainstMe(DateTime? ts) => _ts = ts;
        public override string ToString() => $"Timestamp: [{_ts?.ToString("O") ?? "NONE"}].";
        

        private readonly DateTime? _ts;
    }
}
