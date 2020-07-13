using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace DotNetVault.RefReturningCollections
{
    /// <inheritdoc />
    public sealed class
        BadComparerException<[VaultSafeTypeParam] TComparer, [VaultSafeTypeParam] TValue> : BadComparerException
        where TComparer : struct, IByRefCompleteComparer<TValue>
        where TValue : struct, IEquatable<TValue>, IComparable<TValue>
    {
        /// <inheritdoc />
        public override Type TypeOfComparer { get; }

        /// <inheritdoc />
        public override Type TypeOfComparand { get; }

        /// <summary>
        /// The value of the bad comparer or a default initialized version if none supplied.
        /// </summary>
        public TComparer BadComparer { get; }

        /// <summary>
        /// Create a bad comparer exception.  Comparer value will be default constructed.
        /// </summary>
        public BadComparerException() : this(new TComparer(), null, null)
        {
        }

        /// <summary>
        /// Create a bad comparer exception. Comparer value will be default constructed.
        /// </summary>
        /// <param name="inner">inner exception.</param>
        public BadComparerException(Exception inner) : this(new TComparer(), null, inner)
        {
        }

        /// <summary>
        /// Create a bad comparer exception.  Comparer value will be default constructed.
        /// </summary>
        /// <param name="extraInfo">extra info if any</param>
        public BadComparerException(string extraInfo) : this(new TComparer(), extraInfo, null)
        {
        }

        /// <summary>
        /// Create a bad comparer exception. Comparer value will be default constructed.
        /// </summary>
        /// <param name="extraInfo">extra info if any</param>
        /// <param name="inner">inner exception.</param>
        public BadComparerException(string extraInfo, Exception inner) : this(new TComparer(), extraInfo, inner)
        {
        }

        /// <summary>
        /// Create a bad comparer exception.
        /// </summary>
        /// <param name="comparer">The comparer</param>
        /// <param name="inner">inner exception.</param>
        public BadComparerException(TComparer comparer, Exception inner) : this(comparer, null, inner)
        {
        }

        /// <summary>
        /// Create a bad comparer exception.
        /// </summary>
        /// <param name="comparer">The comparer</param>
        /// <param name="extraInfo">extra info if any</param>
        public BadComparerException(TComparer comparer, string extraInfo) : this(comparer, extraInfo, null)
        {
        }

        /// <summary>
        /// Create a bad comparer exception.
        /// </summary>
        /// <param name="comparer">The comparer</param>
        public BadComparerException(TComparer comparer) : this(comparer, null, null)
        {
        }

        /// <summary>
        /// Create a bad comparer exception.
        /// </summary>
        /// <param name="comparer">The comparer</param>
        /// <param name="extraInfo">extra info if any</param>
        /// <param name="inner">inner exception.</param>
        public BadComparerException(TComparer comparer, [CanBeNull] string extraInfo, [CanBeNull] Exception inner) :
            base(CreateMessage(extraInfo), inner)
        {
            BadComparer = comparer;
            TypeOfComparer = typeof(TComparer);
            TypeOfComparand = typeof(TValue);
        }

        [NotNull]
        private static string CreateMessage([CanBeNull] string extraInfo) =>
            "The supplied comparer is default constructed but comparers of its type do not work properly when default constructed." +
            (!string.IsNullOrWhiteSpace(extraInfo) ? $"  Extra info: [{extraInfo}]." : string.Empty);
    }

    /// <summary>
    /// Thrown when bad comparer used / received.
    /// </summary>
    public abstract class BadComparerException : Exception
    {
        /// <summary>
        /// The type of the comparer
        /// </summary>
        [NotNull]
        public abstract Type TypeOfComparer { get; }

        /// <summary>
        /// The type the comparer compares
        /// </summary>
        [NotNull]
        public abstract Type TypeOfComparand { get; }

        private protected BadComparerException([NotNull] string message, [CanBeNull] Exception inner) : base(message,
            inner)
        {
        }
    }
}