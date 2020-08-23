using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    public sealed class NotARefStruct
    {
        public override string ToString() => "Hi mom!";
    }

    public struct AlsoNotARefStruct
    {
        public override string ToString() => "Bye mom!";
    }

    public ref struct RefStructWithoutAttributeOk
    {
        public string Name { get; set; } 
    }

    public readonly ref struct RoRefStructWithoutAttributeOk
    {
        public string Name { get; }

        RoRefStructWithoutAttributeOk(string name) => Name = name ?? string.Empty;
    }

    [RefStruct]
    public ref struct WithAttribute
    {
        public string Name { get; set; }
    }

    [RefStruct]
    public readonly ref struct RoWithAttr
    {
        public string Name { get; }

        RoWithAttr(string name) => Name = name ?? string.Empty;
    }

    
}
