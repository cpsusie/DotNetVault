using System;
using JetBrains.Annotations;

namespace DotNetVault.Exceptions
{

    /// <summary>
    /// Exception thrown to indicate parameter of value type <typeparamref name="T"/> must
    /// not be negative, but was.
    /// </summary>
    /// <typeparam name="T">The value type of the illegally negative value.</typeparam>
    public sealed class ArgumentNegativeException<T> : ArgumentNegativeException where T : struct
    {
        /// <summary>
        /// The actual typed value of the parameter.
        /// </summary>
        public T TypedValue { get; }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="actualValue">Its actual value</param>
        /// <param name="message">optional additional information</param>
        /// <exception cref="ArgumentNullException"><paramref name="paramName"/> was null.</exception>
        public ArgumentNegativeException([NotNull] string paramName, T actualValue, [CanBeNull] string message) : base(paramName, actualValue, CreateMessage(paramName ?? throw new ArgumentNullException(nameof(paramName)), actualValue, message))
            => TypedValue = actualValue;
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="actualValue">Its actual value</param>
        /// <exception cref="ArgumentNullException"><paramref name="paramName"/> was null.</exception>
        public ArgumentNegativeException([NotNull] string paramName, T actualValue) : this(paramName, actualValue, null) { }

        private static string CreateMessage([NotNull] string paramName, T actualValue, [CanBeNull] string message)
        {
            string baseMsg = "Parameter [" + paramName + "] must be not be negative.  Its actual value was: [" + actualValue + "].";
            if (!string.IsNullOrWhiteSpace(message))
            {
                baseMsg = baseMsg + ("  Extra information: [" + message + "].");
            }
            return baseMsg;
        }
    }

    /// <summary>
    /// Exception thrown to indicate parameter of value type <typeparamref name="T"/> was
    /// not positive, but must be positive.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ArgumentNotPositiveException<T> : ArgumentNotPositiveException where T : struct
    {
        /// <summary>
        /// The actual typed value of the parameter.
        /// </summary>
        public T TypedValue { get; }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="actualValue">Its actual value</param>
        /// <param name="message">optional additional information</param>
        /// <exception cref="ArgumentNullException"><paramref name="paramName"/> was null.</exception>
        public ArgumentNotPositiveException([NotNull] string paramName, T actualValue, [CanBeNull] string message) : base(paramName, actualValue, CreateMessage(paramName ?? throw new ArgumentNullException(nameof(paramName)), actualValue, message ))
            =>  TypedValue = actualValue;
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="actualValue">Its actual value</param>
        /// <exception cref="ArgumentNullException"><paramref name="paramName"/> was null.</exception>
        public ArgumentNotPositiveException([NotNull] string paramName, T actualValue) : this(paramName, actualValue, null) { }

        private static string CreateMessage([NotNull] string paramName, T actualValue, [CanBeNull] string message)
        {
            string baseMsg = "Parameter [" + paramName + "] must be positive.  Its actual value was: [" + actualValue + "].";
            if (!string.IsNullOrWhiteSpace(message))
            {
                baseMsg = baseMsg + ("  Extra information: [" + message + "].");
            }
            return baseMsg;
        }
    }

    /// <summary>
    /// Base class for argument not positive exception
    /// </summary>
    public abstract class ArgumentNotPositiveException : ArgumentOutOfRangeException
    {
        
        private protected ArgumentNotPositiveException(string paramName, object actualValue, string message) : base(paramName, actualValue, message)
        {
        }

        
    }

    /// <summary>
    /// base class for generic ArgumentNegativeException
    /// </summary>
    public abstract class ArgumentNegativeException : ArgumentOutOfRangeException
    {

        private protected ArgumentNegativeException(string paramName, object actualValue, string message) 
            : base(paramName, actualValue, message)
        {
        }


    }
}
