using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace DotNetVault.ExtensionMethods
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<(int Index, T Val)> EnumerateWithIndices<T>([NotNull] this IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            int index = 0;
            foreach (var item in items)
            {
                yield return (index, item);
                ++index;
            }
        }
    }
}
