using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.CustomVaultExamples.CustomLockedResources
{
    /// <summary>
    /// Read-write view of a string builder provided by the readonly lock methods
    /// of the <see cref="StringBufferReadWriteVault"/>.
    /// </summary>
    [NoCopy]
    [RefStruct]
    public readonly ref struct StringBuilderRwLockedResource
    {
        #region Factory Method
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        internal static StringBuilderRwLockedResource CreatedStringBuilderRwLockedResource(
            in CustomReadWriteVaultBase<StringBuilder>.InternalCustomRwLockedResource<
                ReadWriteStringBufferVault.StringBuilderCustomVault> wrappedRes)
        {
            try
            {
                return new StringBuilderRwLockedResource(in wrappedRes);
            }
            catch (Exception ex)
            {
                try
                {
                    wrappedRes.Dispose();
                }
                catch
                {
                    //ignore
                }
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
        } 
        #endregion

        #region Properties
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
        public readonly int Length
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
        #endregion

        #region Private CTOR
        private StringBuilderRwLockedResource(in CustomReadWriteVaultBase<StringBuilder>.InternalCustomRwLockedResource<
            ReadWriteStringBufferVault.StringBuilderCustomVault> toWrap)
        {
            if (toWrap.Value == null) throw new ArgumentException(@"Resource contained in parameter may not be null.", nameof(toWrap));
            _wrapped = toWrap;
            _sb = new StringBuilderRwView(_wrapped.Value);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Query if <paramref name="x"/> is a substring of the string builder.
        /// </summary>
        /// <param name="x">substring to seek</param>
        /// <returns>true if the string builder contains <paramref name="x"/>, false otherwise</returns>
        /// <exception cref="ArgumentNullException"><paramref name="x"/> was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains([NotNull] string x) => IndexOf(x) > -1;
        /// <summary>
        /// Query the string builder to find the first index of x
        /// </summary>
        /// <param name="x">the sought-after substring</param>
        /// <returns>the index where the substring is found, if found, -1 otherwise</returns>
        /// <exception cref="ArgumentNullException">x was null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int IndexOf([NotNull] string x) => _sb.IndexOf(x);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly string ToString(int startIndex, int length) => _sb.ToString(startIndex, length);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string value, int startIndex, int count) => _sb.Append(value, startIndex, count);
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
        public readonly void Clear() => _sb.Clear();
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
        
        /// <summary>
        /// release the lock and return the protected resource to vault for use by others
        /// </summary>
        [NoDirectInvoke]
        [EarlyReleaseJustification(EarlyReleaseReason.CustomWrapperDispose)]
        public void Dispose()
        {
            _wrapped.Dispose();
            _sb.Dispose();
        }
        #endregion

        #region Privates
        private readonly StringBuilderRwView _sb;
        private readonly CustomReadWriteVaultBase<StringBuilder>.InternalCustomRwLockedResource<
            ReadWriteStringBufferVault.StringBuilderCustomVault> _wrapped; 
        #endregion
    }

    /// <summary>
    /// Read-only view of a string builder provided by the readonly lock methods
    /// of the <see cref="StringBufferReadWriteVault"/>.
    /// </summary>
    [NoCopy]
    [RefStruct]
    public ref struct StringBuilderRoLockedResource
    {
        #region Factory
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        internal static StringBuilderRoLockedResource CreatedStringBuilderRoLockedResource(
            in CustomReadWriteVaultBase<StringBuilder>.InternalCustomRoLockedResource<
                ReadWriteStringBufferVault.StringBuilderCustomVault> wrappedRes)
        {
            try
            {
                return new StringBuilderRoLockedResource(in wrappedRes);
            }
            catch (Exception ex)
            {
                try
                {
                    wrappedRes.Dispose();
                }
                catch
                {
                    //ignore
                }
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
        } 
        #endregion

        /// <summary>
        /// Retrieve an individual character of the string builder by index
        /// </summary>
        /// <param name="idx">the index</param>
        /// <exception cref="IndexOutOfRangeException"><paramref name="idx"/> is outside the bounds of the array</exception>
        public readonly char this[int idx] => _roView[idx];
        /// <summary>
        /// The length of string builder
        /// </summary>
        public readonly int Length => _roView.Length;
        /// <summary>
        /// The capacity of the string builder
        /// </summary>
        public readonly int Capacity => _roView.Capacity;
        

        private StringBuilderRoLockedResource(in CustomReadWriteVaultBase<StringBuilder>.InternalCustomRoLockedResource<
            ReadWriteStringBufferVault.StringBuilderCustomVault> toWrap)
        {
            if (toWrap.Value == null) throw new ArgumentException(@"Resource contained in parameter may not be null.", nameof(toWrap));
            _wrapped = toWrap;
            _roView = new StringBuilderRoView(_wrapped.Value);
        }

        #region Public Methods
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
        public readonly int IndexOf([NotNull] string x) => _roView.IndexOf(x);

        /// <inheritdoc />
        public override readonly string ToString() => _roView.ToString();

        /// <summary>
        /// Converts the value of a substring of this instance to a String.
        /// </summary>
        /// <param name="startIndex">index where substring starts</param>
        /// <param name="length">length of substring</param>
        /// <returns>a string that is a substring hereof</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="length"/> is less than zero
        /// -- OR -- sum of <paramref name="startIndex"/> and <paramref name="length"/> is greater than
        /// the size of this StringBuilder</exception>
        public readonly string ToString(int startIndex, int length) => _roView.ToString(startIndex, length);

        /// <summary>
        /// release the lock and return the protected resource to vault for use by others
        /// </summary>
        [NoDirectInvoke]
        [EarlyReleaseJustification(EarlyReleaseReason.CustomWrapperDispose)]
        public void Dispose()
        {
            _wrapped.Dispose();
            _roView.Dispose();
        }
        #endregion

        #region Privates
        private StringBuilderRoView _roView;
        private readonly CustomReadWriteVaultBase<StringBuilder>.InternalCustomRoLockedResource<
            ReadWriteStringBufferVault.StringBuilderCustomVault> _wrapped; 
        #endregion
    }

    /// <summary>
    /// Read-only view of a string builder provided by the readonly lock methods
    /// of the <see cref="StringBufferReadWriteVault"/>.
    /// </summary>
    [NoCopy]
    [RefStruct]
    public ref struct StringBuilderUpgradableRoLockedResource
    {
        #region Factory
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        internal static StringBuilderUpgradableRoLockedResource CreatedStringBuilderUpgradableRoLockedResource(
            in CustomReadWriteVaultBase<StringBuilder>.InternalCustomUpgradableRoLockedResource<
                ReadWriteStringBufferVault.StringBuilderCustomVault> wrappedRes)
        {
            try
            {
                return new StringBuilderUpgradableRoLockedResource(in wrappedRes);
            }
            catch (Exception ex)
            {
                try
                {
                    wrappedRes.Dispose();
                }
                catch
                {
                    //ignore
                }
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
        }
        #endregion

        /// <summary>
        /// Retrieve an individual character of the string builder by index
        /// </summary>
        /// <param name="idx">the index</param>
        /// <exception cref="IndexOutOfRangeException"><paramref name="idx"/> is outside the bounds of the array</exception>
        public readonly char this[int idx] => _roView[idx];
        /// <summary>
        /// The length of string builder
        /// </summary>
        public readonly int Length => _roView.Length;
        /// <summary>
        /// The capacity of the string builder
        /// </summary>
        public readonly int Capacity => _roView.Capacity;


        private StringBuilderUpgradableRoLockedResource(in CustomReadWriteVaultBase<StringBuilder>.InternalCustomUpgradableRoLockedResource<
            ReadWriteStringBufferVault.StringBuilderCustomVault> toWrap)
        {
            if (toWrap.Value == null) throw new ArgumentException(@"Resource contained in parameter may not be null.", nameof(toWrap));
            _wrapped = toWrap;
            _roView = new StringBuilderRoView(_wrapped.Value);
        }

        #region Public Methods
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
        public readonly int IndexOf([NotNull] string x) => _roView.IndexOf(x);

        /// <inheritdoc />
        public override readonly string ToString() => _roView.ToString();

        /// <summary>
        /// Converts the value of a substring of this instance to a String.
        /// </summary>
        /// <param name="startIndex">index where substring starts</param>
        /// <param name="length">length of substring</param>
        /// <returns>a string that is a substring hereof</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="length"/> is less than zero
        /// -- OR -- sum of <paramref name="startIndex"/> and <paramref name="length"/> is greater than
        /// the size of this StringBuilder</exception>
        public readonly string ToString(int startIndex, int length) => _roView.ToString(startIndex, length);

        /// <summary>
        /// release the lock and return the protected resource to vault for use by others
        /// </summary>
        [NoDirectInvoke]
        [EarlyReleaseJustification(EarlyReleaseReason.CustomWrapperDispose)]
        public void Dispose()
        {
            _wrapped.Dispose();
            _roView.Dispose();
        }
        #endregion

        #region Writable Lock acquisition methods
        /// <summary>
        ///  Obtain a writable locked resource.  Keep attempting until
        ///  sooner of following occurs:
        ///     1- time period specified by <paramref name="timeout"/> expires or
        ///     2- cancellation is requested via <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <param name="timeout">the max time to wait for</param>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public readonly StringBuilderRwLockedResource Lock(TimeSpan timeout, CancellationToken token) 
        {
            var intern = _wrapped.Lock(timeout, token);
            return StringBuilderRwLockedResource.CreatedStringBuilderRwLockedResource(in intern);
        }


        /// <summary>
        ///  Obtain a writable locked resource.   Yielding, (not busy), wait.  Keep attempting until
        ///  cancellation is requested via the <paramref name="token"/> parameter's
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="token">a cancellation token</param>
        /// <returns>the resource</returns>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public readonly StringBuilderRwLockedResource Lock(CancellationToken token) 
        {
            var intern = _wrapped.Lock(token);
            return StringBuilderRwLockedResource.CreatedStringBuilderRwLockedResource(in intern);
        }


        /// <summary>
        /// Obtain a writable locked resource.   Yielding, (not busy), wait.
        /// </summary>
        /// <param name="timeout">how long to wait</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> not positive.</exception>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public readonly StringBuilderRwLockedResource Lock(TimeSpan timeout)
        {
            var intern = _wrapped.Lock(timeout);
            return StringBuilderRwLockedResource.CreatedStringBuilderRwLockedResource(in intern);
        }


        /// <summary>
        /// Obtain a writable locked resource.  Yielding, (not busy), wait.  Waits for <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="TimeoutException">didn't obtain resource in time</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public readonly StringBuilderRwLockedResource Lock()
        {
            var intern = _wrapped.Lock();
            return StringBuilderRwLockedResource.CreatedStringBuilderRwLockedResource(in intern);
        }

        /// <summary>
        /// Obtain a writable locked resource.   This call can potentially block forever, unlike the
        /// other methods this vault exposes.  It may sometimes be desireable from a performance perspective
        /// not to check every so often for time expiration or cancellation requests.  For that reason, this explicitly
        /// named method exists.  Using this method, however, may cause a dead lock under certain circumstances (e.g.,
        /// you acquire this lock and another lock in different orders on different threads)
        /// </summary>
        /// <returns>the resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread calling this function already held the lock.</exception>
        [return: UsingMandatory]
        public readonly StringBuilderRwLockedResource LockBlockUntilAcquired()
        {
            var intern = _wrapped.LockWaitForever();
            return StringBuilderRwLockedResource.CreatedStringBuilderRwLockedResource(in intern);
        }
        #endregion

        #region Privates
        private StringBuilderRoView _roView;
        private readonly CustomReadWriteVaultBase<StringBuilder>.InternalCustomUpgradableRoLockedResource<
            ReadWriteStringBufferVault.StringBuilderCustomVault> _wrapped;
        #endregion
    }

    [RefStruct]
    internal ref struct SubStringMatcher
    {
        public static int FindFirstIndexOfSubString([NotNull] StringBuilder str, [NotNull] string substr)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            if (substr == null) throw new ArgumentNullException(nameof(substr));
            if (substr.Length > str.Length) return -1;
            if (str.Length == 0 || substr.Length == 0) return -1;
            SubStringMatcher matcher = CreateMatcher(substr);
            int idx = -1;
            for (int i = 0; i < str.Length; ++i)
            {
                matcher.FeedChar(str[i]);
                if (matcher.IsMatch)
                {
                    idx = i - (matcher.Length - 1);
                    break;
                }
            }
            return idx;
        }
        public static SubStringMatcher CreateMatcher(string subStringToMatch)
        {
            var span = subStringToMatch.AsSpan();
            return new SubStringMatcher(in span);
        }
        public static SubStringMatcher CreateMatcher(in ReadOnlySpan<char> subStringToMatch)
            => new SubStringMatcher(in subStringToMatch);
        public int Length => _stringText.Length;
        public bool IsMatch => _isMatch;
        public void FeedChar(char c)
        {
            if (_isMatch)
            {
                _state = 0;
                _isMatch = false;
            }
            if (c == _stringText[_state])
            {
                ++_state;
                if (_state == _stringText.Length)
                {
                    _isMatch = true;
                }
            }
        }
        private SubStringMatcher(in ReadOnlySpan<char> span)
        {
            if (span.Length < 1)
                throw new ArgumentException(@"Empty span not allowed.", nameof(span));
            _stringText = span;
            _state = 0;
            _isMatch = false;
        }
        private bool _isMatch;
        private int _state;
        private readonly ReadOnlySpan<char> _stringText;
    }
}

