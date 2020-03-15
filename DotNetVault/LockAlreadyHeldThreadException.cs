using System;
using JetBrains.Annotations;

namespace DotNetVault
{
    /// <summary>
    /// This exception signifies that an attempt was made to acquire the lock recursively:
    /// on a thread that already held the lock, an attempt was made to acquire it again.
    /// </summary>
    public class LockAlreadyHeldThreadException : InvalidOperationException
    {
        #region Public Properties
        /// <summary>
        /// The Thread Id of the thread that already held the lock
        /// when an attempt to obtain it again was made.
        /// </summary>
        public int ThreadId => _threadId; 
        #endregion

        #region CTORS
        /// <summary>
        /// Create a LockAlreadyHeldThreadException
        /// </summary>
        /// <param name="inner">inner exception</param>
        /// <param name="threadId">thread id</param>
        public LockAlreadyHeldThreadException(int threadId, Exception inner)
            : this(threadId, null, inner) { }

        /// <summary>
        /// Create a LockAlreadyHeldThreadException
        /// </summary>
        /// <param name="message">the message</param>
        /// <param name="threadId">thread id</param>
        public LockAlreadyHeldThreadException(int threadId, string message)
            : this(threadId, message, null) { }

        /// <summary>
        /// Create a LockAlreadyHeldThreadException
        /// </summary>
        /// <param name="threadId">the thread id</param>
        public LockAlreadyHeldThreadException(int threadId)
            : this(threadId, null, null) { }

        /// <summary>
        /// Create a LockAlreadyHeldThreadException
        /// </summary>
        /// <param name="message">the message</param>
        /// <param name="innerException">inner exception</param>
        /// <param name="threadId">thread id</param>
        public LockAlreadyHeldThreadException(int threadId, [CanBeNull] string message, [CanBeNull] Exception innerException)
            : base(CreateMessage(message, threadId), innerException) => _threadId = threadId;

        /// <summary>
        /// Intended for use with derived classes if any. Unlike other ctors the message
        /// passed onto base is EXACTLY the content of <paramref name="message"/>
        /// </summary>
        /// <param name="inner">Inner Exception</param>
        /// <param name="threadId">thread id</param>
        /// <param name="message">message</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> was null.</exception>
        protected LockAlreadyHeldThreadException(Exception inner, int threadId, [NotNull] string message) 
            : base(message ?? throw new ArgumentNullException(nameof(message)), inner) =>
                _threadId = threadId;
        
        #endregion

        #region Private Method
        private static string CreateMessage(string message, int threadId)
        {
            string extraInfo = !string.IsNullOrWhiteSpace(message) ? $"  Additional info: [{message}]." : string.Empty;
            return string.Format(MessageFormatString, threadId, extraInfo);
        } 
        #endregion

        #region Private Data
        private readonly int _threadId;
        /// <summary>
        /// used for constructing message.  0 - thread id, 1- info passed in message
        /// parameter to ctor
        /// </summary>
        protected const string MessageFormatString = "The thread with id {0} already has acquired the lock.{1}"; 
        #endregion
    }
}