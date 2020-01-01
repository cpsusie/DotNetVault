using System;
using JetBrains.Annotations;

namespace DotNetVault.Exceptions
{
    /// <summary>
    /// An exception that signals that a delegate did not behave in the way its caller expected.
    /// </summary>
    public abstract class DelegateException : Exception
    {
        /// <summary>
        /// The name of the offending delegate
        /// </summary>
        [NotNull]
        public string DelegateName { get; }
        /// <summary>
        /// A reference to the offending delegate
        /// </summary>
        [NotNull]
        protected abstract Delegate OffendingDelegateBase { get; }

        /// <summary>
        /// Create a delegate exception
        /// </summary>
        /// <param name="delegateName">the delegate's name</param>
        /// <param name="message">an error message</param>
        /// <param name="inner">the inner exception</param>
        /// <exception cref="ArgumentNullException"><paramref name="delegateName"/> or <paramref name="message"/>
        /// was null.</exception>
        protected DelegateException([NotNull] string delegateName, [NotNull] string message,
            [CanBeNull] Exception inner) : base(message ?? throw new ArgumentNullException(nameof(message)), inner)
            => DelegateName = delegateName ?? throw new ArgumentNullException(nameof(delegateName));
    }

    /// <summary>
    /// Generic version of <see cref="DelegateException"/>
    /// </summary>
    /// <typeparam name="TDelegate">the delegate type.</typeparam>
    public abstract class DelegateException<TDelegate> : DelegateException where TDelegate : Delegate
    {
        /// <summary>
        /// A fully typed reference to the offending delegate
        /// </summary>
        [NotNull]
        public TDelegate OffendingDelegate { get; }

        /// <inheritdoc />
        protected sealed override Delegate OffendingDelegateBase => OffendingDelegate;

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="del">the offending delegate</param>
        /// <param name="delegateName">name of offending delegate</param>
        /// <param name="message">message</param>
        /// <param name="inner">inner exception, if any</param>
        /// <exception cref="ArgumentNullException"><paramref name="del"/> or <paramref name="delegateName"/> was null.</exception>
        protected DelegateException([NotNull] TDelegate del, [NotNull] string delegateName, [NotNull] string message,
            [CanBeNull] Exception inner) : base(delegateName, message, inner) =>
            OffendingDelegate = del ?? throw new ArgumentNullException(nameof(del));
    }

    /// <summary>
    /// Thrown to indicate that the delegate threw an exception,
    /// contrary to expectations of caller
    /// </summary>
    /// <typeparam name="TDelegate">the type of the offending delegate</typeparam>
    public class DelegateThrewException<TDelegate> : DelegateException<TDelegate> where TDelegate : Delegate
    {

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="del">the delegate that threw an exception</param>
        /// <param name="delegateName">the name of the delegate that threw an exception</param>
        /// <param name="inner">the exception the delegate threw</param>
        /// <exception cref="ArgumentNullException">one or more of the parameters was null.</exception>
        public DelegateThrewException([NotNull] TDelegate del, [NotNull] string delegateName, [NotNull] Exception inner)
            : base(del, delegateName, CreateMessage(
                delegateName ?? throw new ArgumentNullException(nameof(delegateName)),
                inner ?? throw new ArgumentNullException(nameof(inner))), inner) {}

        /// <summary>
        /// Used by CTOR to create a message to forward on to the its base class
        /// </summary>
        /// <param name="delegateName">the delegate name</param>
        /// <param name="thrownEx">the exception it thrown</param>
        /// <returns>the message the ctor should forward on to its base class.</returns>
        protected static string CreateMessage(string delegateName, Exception thrownEx) =>
            string.Format(MessageBaseFormat, delegateName, typeof(TDelegate).Name, thrownEx.GetType().Name);

        private const string MessageBaseFormat = "The delegate [{0}] of type [{1}] threw an exception of type [{2}].";
    }

    /// <summary>
    /// Exception that indicates that an exception returned null, contrary
    /// to caller expectations
    /// </summary>
    /// <typeparam name="TDelegate">the type of the offending delegate</typeparam>
    public class DelegateReturnedNullException<TDelegate> : DelegateException<TDelegate> where TDelegate : Delegate
    {
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="del">the delegate that returned null</param>
        /// <param name="delegateName">the name of the delegate that returned null</param>
        /// <exception cref="ArgumentNullException"><paramref name="del"/> or <paramref name="delegateName"/>
        /// returned null.</exception>
        public DelegateReturnedNullException([NotNull] TDelegate del, [NotNull] string delegateName) : base(del,
            delegateName, 
            CreateMessage(delegateName ?? throw new ArgumentNullException(nameof(delegateName))), 
            null) {}
        
        /// <summary>
        /// Used by ctor to generate message to forward to
        /// base class.
        /// </summary>
        /// <param name="delegateName">the name of the delegate</param>
        /// <returns>the string the ctor to forward on to its base class</returns>
        protected static string CreateMessage(string delegateName) =>
            string.Format(MessageBaseFormat, delegateName, typeof(TDelegate).Name);

        private const string MessageBaseFormat = "The delegate [{0}] of type [{1}] returned a null reference.";
    }


}
