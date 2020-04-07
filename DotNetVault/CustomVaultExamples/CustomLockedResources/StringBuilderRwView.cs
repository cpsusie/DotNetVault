using System;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;

namespace DotNetVault.CustomVaultExamples.CustomLockedResources
{
    /// <summary>
    /// Provides a readonly view of a string builder
    /// </summary>
    internal readonly ref struct StringBuilderRwView
    {
        /// <summary>
        /// Retrieve an individual character of the string builder by index
        /// </summary>
        /// <param name="idx">the index</param>
        /// <exception cref="IndexOutOfRangeException"><paramref name="idx"/> is outside the bounds of the array
        /// (read ops)</exception>
        /// /// <exception cref="ArgumentOutOfRangeException"><paramref name="idx"/> is outside the bounds of the array
        /// (write ops)</exception>
        public char this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _sb[idx];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _sb[idx] = value; 
        }

        /// <summary>
        /// The length of string builder
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _sb.Length; 
        }

        /// <summary>
        /// The capacity of the string builder
        /// </summary>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _sb.Capacity;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _sb.Capacity = value;
        }

        /// <summary>
        /// Query if <paramref name="x"/> is a substring of the string builder.
        /// </summary>
        /// <param name="x">substring to seek</param>
        /// <returns>true if the string builder contains <paramref name="x"/>, false otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="x"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains([NotNull] string x) => IndexOf(x) > -1;

        /// <summary>
        /// Query the string builder to find the first index of x
        /// </summary>
        /// <param name="x">the sought-after substring</param>
        /// <returns>the index where the substring is found, if found, -1 otherwise</returns>
        /// <exception cref="ArgumentNullException">x was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf([NotNull] string x)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (_sb.Length == 0) return -1;
            if (x.Length == 0) return -1;
            return SubStringMatcher.FindFirstIndexOfSubString(_sb, x);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => _sb.ToString();

        /// <summary>
        /// Converts the value of a substring of this instance to a String.
        /// </summary>
        /// <param name="startIndex">index where substring starts</param>
        /// <param name="length">length of substring</param>
        /// <returns>a string that is a substring hereof</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="length"/> is less than zero
        /// -- OR -- sum of <paramref name="startIndex"/> and <paramref name="length"/> is greater than
        /// the size of this StringBuilder</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(int startIndex, int length) => _sb.ToString(startIndex, length);

        /// <summary>
        /// Appends a specified number of copies of the string representation of a Unicode
        /// character to this instance.
        /// </summary>
        /// <param name="value">The character to append.</param>
        /// <param name="repeatCount">The number of times to append value.</param>
        /// <exception cref="ArgumentOutOfRangeException"> <paramref name="repeatCount"/> is less than zero. -or-
        /// Enlarging the value of this instance would exceed <see cref="System.Text.StringBuilder.MaxCapacity"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char value, int repeatCount) => _sb.Append(value, repeatCount);

        /// <summary>
        ///  Appends the string representation of a specified Boolean value to this instance.
        /// </summary>
        /// <param name="value">The Boolean value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(bool value) => _sb.Append(value);
        /// <summary>
        ///   Appends the string representation of a specified System.Char object to this instance.
        /// </summary>
        /// <param name="value">The UTF-16-encoded code unit to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified 64-bit unsigned integer to this
        /// instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ulong value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified 32-bit unsigned integer to this
        /// instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(uint value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified 8-bit unsigned integer to this
        /// instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(byte value) => _sb.Append(value);

        /// <summary>
        /// Appends a copy of a specified substring to this instance.
        /// </summary>
        /// <param name="value">The string that contains the substring to append.</param>
        /// <param name="startIndex">The starting position of the substring within value.</param>
        /// <param name="count">The number of characters in value to append.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/> and
        /// <paramref name="startIndex"/> and <paramref name="count"/> are not both zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> less than zero. -or-
        /// <paramref name="count"/> is less than zero. -or-
        /// <paramref name="startIndex"/> + <paramref name="count"/> is greater than the <see cref="String.Length"/> of
        /// <paramref name="value"/> -or-
        /// Enlarging the value of this instance would exceed
        /// <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Append(string value, int startIndex, int count) => _sb.Append(value, startIndex, count);
        /// <summary>
        /// Appends a copy of the specified string to this instance.
        /// </summary>
        /// <param name="value">The string to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string value) => _sb.Append(value);
        /// <summary>
        /// Appends the string representation of a specified single-precision floating-point
        /// number to this instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(float value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified 16-bit unsigned integer to this
        /// instance.
        ///</summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        
        public void Append(ushort value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of the Unicode characters in a specified array
        /// to this instance.
        /// </summary>
        /// <param name="value">The array of characters to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would exceed
        /// StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char[] value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified sub-array of Unicode characters
        /// to this instance.
        /// </summary>
        /// <param name="value">A character array.</param>
        /// <param name="startIndex">The starting position in value.</param>
        /// <param name="charCount">The number of characters to append.</param>
        /// <exception cref="ArgumentNullException">value is null, and startIndex and charCount are not zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException">charCount is less than zero. -or- startIndex is less than zero. -or- startIndex
        /// + charCount is greater than the length of value. -or- Enlarging the value of
        /// this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char[] value, int startIndex, int charCount) => _sb.Append(value, startIndex, charCount);

        /// <summary>
        /// Appends the string representation of a specified 8-bit signed integer to this
        /// instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(sbyte value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified decimal number to this instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(decimal value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified 16-bit signed integer to this
        /// instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(short value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified 32-bit signed integer to this
        /// instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified 64-bit signed integer to this
        /// instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(long value) => _sb.Append(value);

        /// <summary>
        /// Appends the string representation of a specified double-precision floating-point
        /// number to this instance.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(double value) => _sb.Append(value);

        /// <summary>
        /// Appends the default line terminator to the end of the current object.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLine() => _sb.AppendLine();

        /// <summary>
        /// Appends a copy of the specified string followed by the default line terminator
        /// to the end of the current System.Text.StringBuilder object.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would
        /// exceed <see cref="StringBuilder.MaxCapacity"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLine(string value) => _sb.AppendLine(value);

        /// <summary>
        /// Removes all characters from the current System.Text.StringBuilder instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _sb.Clear();

        /// <summary>
        /// Copies the characters from a specified segment of this instance to a specified
        /// segment of a destination System.Char array.
        /// </summary>
        /// <param name="sourceIndex">The starting position in this instance where characters will be copied from.</param>
        /// <param name="destination">The array where characters will be copied.</param>
        /// <param name="destinationIndex">The starting position in destination where characters will be copied.</param>
        /// <param name="count">The number of characters to be copied.</param>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> was <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceIndex"/>, <paramref name="destinationIndex"/> or <paramref name="count"/>
        /// is less than zero.  -or- <paramref name="sourceIndex"/> is greater than the <see cref="Length"/> of this object.</exception>
        /// <exception cref="ArgumentException"><paramref name="sourceIndex"/> + <paramref name="count"/> is greater than the
        /// <see cref="Length"/> of this object or <paramref name="destinationIndex"/> +
        /// <paramref name="count"/> is greater than the <see cref="Array.Length"/> of
        /// <paramref name="destination"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) =>
            _sb.CopyTo(sourceIndex, destination, destinationIndex, count);

        /// <summary>
        /// Ensures that the capacity of this instance of System.Text.StringBuilder is at
        /// least equal to <paramref name="capacity"/>.
        /// </summary>
        /// <param name="capacity">The minimum capacity to ensure.</param>
        /// <returns>The new capacity of this instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.
        /// -or- Enlarging the value of this instance would exceed <see cref="StringBuilder.MaxCapacity"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnsureCapacity(int capacity) => _sb.EnsureCapacity(capacity);

        /// <summary>
        /// Inserts the string representation of a specified sub-array of Unicode characters
        /// into this instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">A character array.</param>
        /// <param name="startIndex">The starting index within value.</param>
        /// <param name="charCount">The number of characters to insert.</param>
        /// <exception cref="ArgumentNullException">value is null, and startIndex and charCount are not zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// index, startIndex, or charCount is less than zero. -or- index is greater than
        /// the length of this instance. -or- startIndex plus charCount is not a position
        /// within value. -or- Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, char[] value, int startIndex, int charCount) =>
            _sb.Insert(index, value, startIndex, charCount);

        /// <summary>
        /// Inserts the string representation of a Boolean value into this instance at the
        /// specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, bool value) => _sb.Insert(index, value);

        /// <summary>
        /// Inserts the string representation of a specified 8-bit unsigned integer into
        /// this instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, byte value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a 64-bit unsigned integer into this instance
        /// at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, ulong value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a specified array of Unicode characters
        /// into this instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, char[] value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a 16-bit unsigned integer into this instance
        /// at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, ushort value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts one or more copies of a specified string into this instance at the specified
        ///  character position.
        /// </summary>
        /// <param name="count">The number of times to insert value.</param>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance
        /// -or- count is less than zero.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would
        /// exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, string value, int count) => _sb.Insert(index, value, count);
        /// <summary>
        /// Inserts the string representation of a specified Unicode character into this
        /// instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, char value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a 32-bit unsigned integer into this instance
        /// at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, uint value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a specified 8-bit signed integer into this
        /// instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, sbyte value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a specified 32-bit signed integer into this
        /// instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, long value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a specified 32-bit signed integer into this
        /// instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, int value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a specified 16-bit signed integer into this
        /// instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, short value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a double-precision floating-point number
        /// into this instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, double value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a decimal number into this instance at the
        /// specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, decimal value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts the string representation of a single-precision floating point number
        /// into this instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance.</exception>
        /// <exception cref="OutOfMemoryException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, float value) => _sb.Insert(index, value);
        /// <summary>
        /// Inserts a string into this instance at the specified character position.
        /// </summary>
        /// <param name="index">The position in this instance where insertion begins.</param>
        /// <param name="value">The value to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero or greater than the length of this instance --
        /// or -- Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, string value) => _sb.Insert(index, value);
        /// <summary>
        /// Removes the specified range of characters from this instance.
        /// </summary>
        /// <param name="startIndex">The index where the removal should begin.</param>
        /// <param name="length">The number of characters to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">startIndex or length is less than zero, or startIndex + length is greater
        /// than the length of this instance.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int startIndex, int length) => _sb.Remove(startIndex, length);
        /// <summary>
        /// Replaces all occurrences of a specified character in this instance with another
        /// specified character.
        /// </summary>
        /// <param name="oldChar">The character to replace.</param>
        /// <param name="newChar"> The character that replaces oldChar.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replace(char oldChar, char newChar) => _sb.Replace(oldChar, newChar);
        /// <summary>
        /// Replaces, within a substring of this instance, all occurrences of a specified
        /// character with another specified character.
        /// </summary>
        /// <param name="oldChar">The character to replace.</param>
        /// <param name="newChar">The character that replaces oldChar.</param>
        /// <param name="startIndex">The position in this instance where the substring begins.</param>
        /// <param name="count">The length of the substring.</param>
        /// <exception cref="ArgumentOutOfRangeException"> startIndex + count is greater than the length of the value of this instance.
        /// -or- startIndex or count is less than zero.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replace(char oldChar, char newChar, int startIndex, int count) =>
            _sb.Replace(oldChar, newChar, startIndex, count);
        /// <summary>
        /// Replaces all occurrences of a specified string in this instance with another
        /// specified string.
        /// </summary>
        /// <param name="oldValue">The string to replace.</param>
        /// <param name="newValue">The string that replaces oldValue, or null.</param>
        /// <exception cref="ArgumentNullException">oldValue is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="oldValue"/> has a <see cref="String.Length"/> of zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Enlarging the value of this instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replace(string oldValue, string newValue) => _sb.Replace(oldValue, newValue);
        /// <summary>
        /// Replaces, within a substring of this instance, all occurrences of a specified
        /// string with another specified string. 
        /// </summary>
        /// <param name="oldValue">The string to replace.</param>
        /// <param name="newValue">The string that replaces oldValue, or null.</param>
        /// <param name="startIndex">The position in this instance where the substring begins.</param>
        /// <param name="count">The length of the substring.</param>
        /// <exception cref="ArgumentNullException">oldValue is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="oldValue"/> has a <see cref="string.Length"/> of zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException">startIndex or count is less than zero. -or- startIndex plus count indicates a
        /// character position not within this instance. -or- Enlarging the value of this
        /// instance would exceed System.Text.StringBuilder.MaxCapacity.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replace(string oldValue, string newValue, int startIndex, int count) =>
            _sb.Replace(oldValue, newValue, startIndex, count);

        internal StringBuilderRwView([NotNull] StringBuilder sb) =>
            _sb = sb ?? throw new ArgumentNullException(nameof(sb));

        /// <summary>
        /// Clear out contents
        /// </summary>
        public void Dispose()
        {
            
        }

        private readonly StringBuilder _sb;
    }
}