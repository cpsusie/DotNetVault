using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [RefStruct]
    public readonly struct RoRegularNotARefStructCase2
    {
        public string Name { get; }

        RoRegularNotARefStructCase2(string name) => Name = name ?? string.Empty;
    }
}
