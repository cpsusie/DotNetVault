using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;

namespace DotNetVault.Test.TestCases
{
    public interface IVsTpTypeOwner<[VaultSafeTypeParam] T>
    {
        Type TypeInfo { get; }
    }

    public class VsTpTypeOwner<[VaultSafeTypeParam] T> : IVsTpTypeOwner<T>
    {
        public Type TypeInfo => typeof(T);
    }

    public class JustOneVsTypeOwner<TNotVaultSafe, [VaultSafeTypeParam] TVaultSafe> : IVsTpTypeOwner<TVaultSafe>
    {
        public Type PossiblyUnsafeType => typeof(TNotVaultSafe);

        public Type TypeInfo => typeof(TVaultSafe);
    }

    public sealed class JustSbIntOwner : JustOneVsTypeOwner<StringBuilder, Categories>
    {

    }

    public class VsIntOwner : VsTpTypeOwner<int>
    {

    }

    public sealed class IntVpTpOwner : IVsTpTypeOwner<int>
    {
        public Type TypeInfo => typeof(int);
    }

    public static class UsesTpOwner
    {
        public static void DoStuff()
        {
            IVsTpTypeOwner<int> vstpOwner = new VsTpTypeOwner<int>();
            VsIntOwner ow = new VsIntOwner();
            JustSbIntOwner gggg = new JustSbIntOwner();
            if (vstpOwner.TypeInfo != typeof(int)) throw new Exception();
            if (ow.TypeInfo != typeof(int)) throw new Exception();
            if (gggg.TypeInfo != typeof(Categories)) throw new Exception();
        }
    }

    public enum Categories
    {
        Awesome,
        Sucky
    }
}
