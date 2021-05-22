using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [VaultSafe]
    struct Issue8TestCase2Similar
    {
        public int X;
        public int Y;
        public int Z;

        public Issue8TestCase2Similar(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    [VaultSafe]
    public sealed class TestMe
    {
        public TestMe(int x, int y, int z) => _value = new Issue8TestCase2Similar(x, y, z);

        public override string ToString() =>
            $"[{nameof(TestMe)}] -- X: [{_value.X}]; Y: [{_value.Y}]; Z: [{_value.Z}].";

        private readonly Issue8TestCase2Similar _value;
    }
}
