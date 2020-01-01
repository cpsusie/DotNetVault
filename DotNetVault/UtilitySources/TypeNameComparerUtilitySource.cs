using System;
using DotNetVault.Logging;
using JetBrains.Annotations;

namespace DotNetVault.UtilitySources
{
    internal static class TypeNameComparerUtilitySource
    {
        public static StringComparer TypeNameComparer => TheTypeNameComparer;

        public static bool TrySupplyNonDefaultComparer([NotNull] StringComparer alternate) =>
            TheTypeNameComparer.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        private static readonly LocklessWriteOnce<StringComparer> TheTypeNameComparer =
            new LocklessWriteOnce<StringComparer>(() => StringComparer.Ordinal);
    }
}
