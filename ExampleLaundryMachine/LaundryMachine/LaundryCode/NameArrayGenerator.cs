using System;
using System.Linq;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public static class NameArrayGenerator
    {
        public static string[] CreateRandomNames([NotNull] Random r, int entries)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            string names = Names.names;
            names = names.Replace("\r\n", "\n");
            names = names.Replace('\r', '\n');
            string[] arr = names.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            arr = arr.Where(str => !string.IsNullOrWhiteSpace(str)).Select(str => str.Trim()).ToArray();
            if (arr.Length < entries)
                throw new ArgumentOutOfRangeException(nameof(entries), entries,
                    @$"Only {arr.Length} entries are available.");
            arr = arr.Take(entries).ToArray();
            r.Shuffle(arr);
            return arr;
        }

        public static void Shuffle<T>(this Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }
}
