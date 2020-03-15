using System;
using System.Threading;
using JetBrains.Annotations;

namespace DotNetVault
{
    /// <summary>
    /// Used to wrap lock recursion exceptions thrown by read write vaults
    /// </summary>
    public class RwLockAlreadyHeldThreadException : LockAlreadyHeldThreadException
    {
        #region Public Properties
        /// <summary>
        /// The inner exception thrown by the internal ReadWrite lock facilities
        /// </summary>
        [NotNull] public LockRecursionException InnerLockRecursionException { get; }
        #endregion

        #region CTORS
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="threadId">thread id of thread where ex thrown from</param>
        /// <param name="inner">lock recursion exception</param>
        /// <exception cref="ArgumentNullException"><paramref name="inner"/> was null.</exception>
        public RwLockAlreadyHeldThreadException(int threadId, [NotNull] LockRecursionException inner)
            : this(null, threadId, inner) { }
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="message">optional additional message</param>
        /// <param name="threadId">thread id of thread where ex thrown from</param>
        /// <param name="inner">lock recursion exception</param>
        /// <exception cref="ArgumentNullException"><paramref name="inner"/> was null.</exception>
        public RwLockAlreadyHeldThreadException([CanBeNull] string message, int threadId,
            [NotNull] LockRecursionException inner)
            : base(inner ?? throw new ArgumentNullException(nameof(inner)),
                threadId, CreateMessage(message, threadId, inner)) =>
                    InnerLockRecursionException = inner;
        #endregion

        #region Private Methods
        [NotNull] private static string CreateMessage([CanBeNull] string message, int threadId,
            [NotNull] LockRecursionException ex)
        {
            string extraInfo = !string.IsNullOrWhiteSpace(message) ? ("  " + message) : string.Empty;
            string innerExMsg = !string.IsNullOrWhiteSpace(ex.Message)
                ? ("  Msg from inner exception:  [" + ex.Message + "].")
                : string.Empty;
            return string.Format(MessageFormatString, threadId, extraInfo) + innerExMsg;
        }
        #endregion
    }
}