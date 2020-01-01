using System;
using DotNetVault.Attributes;

namespace LaundryMachine.LaundryCode
{
    public static class EnumExtensions
    {
        public static bool IsValueDefined<[VaultSafeTypeParam] TEnum>(this TEnum e) where TEnum : unmanaged, Enum =>
            EnumSet<TEnum>.GetInstance().IsDefined(e);
    }
}