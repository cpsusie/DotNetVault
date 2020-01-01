using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using DotNetVault.Attributes;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe(true)]
    public sealed class EnumSet<[VaultSafeTypeParam] TEnum> where TEnum : unmanaged, Enum
    {
        public static EnumSet<TEnum> GetInstance()
        {
            //slight chance there might end up being two instances, but don't really care that much
            //unless changed frequently, shouldn't be a problem.
            var ret = TheValue.Value;
            if (ret == null)
            {
                var temp = new EnumSet<TEnum>();
                TheValue.TrySet(temp);
                ret = TheValue.Value ?? temp;
            }
            Debug.Assert(ret != null);
            return ret;
        }

        public static bool ReleaseCache()
        {
            return TheValue.TryClear();
        }

        public bool IsDefined(TEnum enV) => _lookup.Contains(enV);
        
        private EnumSet()
        {
            _lookup = ImmutableHashSet.Create<TEnum>(Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToArray());
        }

        private readonly ImmutableHashSet<TEnum> _lookup;
        private static readonly LocklessSetClearDispose<EnumSet<TEnum>> TheValue = new LocklessSetClearDispose<EnumSet<TEnum>>();
    }
    

}
