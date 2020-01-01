using System;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace DotNetVault.Miscellaneous
{
    internal static class AnimalNameTextComparerSource
    {
        public static StringComparer AnimalNameTextComparer
        {
            get
            {
                StringComparer ret = _theComparer;
                if (ret == null)
                {
                    var newComparer = StringComparer.CurrentCulture;
                    Debug.Assert(newComparer != null);
                    Interlocked.CompareExchange(ref _theComparer, newComparer, null);
                    ret = _theComparer;
                    Debug.Assert(ret != null);
                }
                return ret;
            }
        }

        public static bool SupplyNonDefaultComparer([NotNull] StringComparer nonDefaultComparer)
        {
            if (nonDefaultComparer == null) throw new ArgumentNullException(nameof(nonDefaultComparer));
            StringComparer old = Interlocked.CompareExchange(ref _theComparer, nonDefaultComparer, null);
            return old == null;
        }

        private static volatile StringComparer _theComparer;
    }
}
