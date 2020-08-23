using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    [RefStruct]
    public struct RegularNotARefStruct
    {
        public string Name { get; set; }
    }



    //compiler takes care of this already
    //[RefStruct]
    //public sealed class Foobar
    //{
    //    public string Name { get; set; } = "George";
    //}

    //[RefStruct]
    //public enum Foobar
    //{
    //    Hi
    //}
    //[RefStruct]
    //public interface IMDefinitelyNotARefStruct
    //{
    //    string Name { get; }
    //}

}
