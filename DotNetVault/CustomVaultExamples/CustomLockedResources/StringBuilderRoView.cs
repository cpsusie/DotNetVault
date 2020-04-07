using System;
using System.Text;
using JetBrains.Annotations;

namespace DotNetVault.CustomVaultExamples.CustomLockedResources
{
    /// <summary>
    /// Provides a readonly view of a string builder
    /// </summary>
    internal ref struct StringBuilderRoView
    {
        /// <summary>
        /// Retrieve an individual character of the string builder by index
        /// </summary>
        /// <param name="idx">the index</param>
        /// <exception cref="IndexOutOfRangeException"><paramref name="idx"/> is outside the bounds of the array</exception>
        public readonly char this[int idx] => _sb[idx];
        /// <summary>
        /// The length of string builder
        /// </summary>
        public readonly int Length => _sb.Length;
        /// <summary>
        /// The capacity of the string builder
        /// </summary>
        public readonly int Capacity => _sb.Capacity;
        /// <summary>
        /// Query if <paramref name="x"/> is a substring of the string builder.
        /// </summary>
        /// <param name="x">substring to seek</param>
        /// <returns>true if the string builder contains <paramref name="x"/>, false otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="x"/> was null.</exception>
        public readonly bool Contains([NotNull] string x) => IndexOf(x) > -1;
        /// <summary>
        /// Query the string builder to find the first index of x
        /// </summary>
        /// <param name="x">the sought-after substring</param>
        /// <returns>the index where the substring is found, if found, -1 otherwise</returns>
        /// <exception cref="ArgumentNullException">x was null.</exception>
        public readonly int IndexOf([NotNull] string x)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (_sb.Length == 0) return -1;
            if (x.Length == 0) return -1;
            return SubStringMatcher.FindFirstIndexOfSubString(_sb, x);
        }
        /// <inheritdoc />
        public override readonly string ToString() => _sb.ToString();
        /// <summary>
        /// Converts the value of a substring of this instance to a String.
        /// </summary>
        /// <param name="startIndex">index where substring starts</param>
        /// <param name="length">length of substring</param>
        /// <returns>a string that is a substring hereof</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="length"/> is less than zero
        /// -- OR -- sum of <paramref name="startIndex"/> and <paramref name="length"/> is greater than
        /// the size of this StringBuilder</exception>
        public readonly string ToString(int startIndex, int length) => _sb.ToString(startIndex, length);
        
        internal StringBuilderRoView([NotNull] StringBuilder sb) =>
            _sb = sb ?? throw new ArgumentNullException(nameof(sb));
        
        /// <summary>
        /// Clear out contents
        /// </summary>
        public void Dispose()
        {
            _sb = null;
        }

        private StringBuilder _sb;
    }
}