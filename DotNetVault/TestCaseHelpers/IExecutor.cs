using System;
using JetBrains.Annotations;

namespace DotNetVault.TestCaseHelpers
{
    /// <summary>
    /// Interface used to create threads that process actions sequentially
    /// </summary>
    public interface IExecutor : IDisposable
    {
        /// <summary>
        /// Has the thread ever been started
        /// </summary>
        bool Started { get; }
        /// <summary>
        /// is the thread in a faulted state?
        /// </summary>
        bool Faulted { get; }
        /// <summary>
        /// Has the thread been terminated
        /// </summary>
        bool Terminated { get; }
        /// <summary>
        /// Has this object been exposed.
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// Enqueue an action for the thread to execute
        /// </summary>
        /// <param name="a">the action you want the thread to execute</param>
        /// <exception cref="ArgumentNullException"><paramref name="a"/> was <see langword="null"/></exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed</exception>
        /// <exception cref="Exception">The object is in a faulted state</exception>
        void EnqueueAction([NotNull] Action a);
        
    }

    /// <summary>
    /// Factory for creating executors
    /// </summary>
    public readonly struct ExecutorFactory
    {
        /// <summary>
        /// Create an executor with this factory
        /// </summary>
        /// <param name="namePrefix">the thread name prefix</param>
        /// <returns>An executor</returns>
        /// <exception cref="ArgumentNullException"><paramref name="namePrefix"/> was null</exception>
        [NotNull] public IExecutor CreateExecutor([NotNull] string namePrefix) =>
            Executor.CreateExecutor(namePrefix ?? throw new ArgumentNullException(nameof(namePrefix)));

        /// <summary>
        /// Create an executor that calibrates the time stamp source when the thread starts
        /// </summary>
        /// <param name="namePrefix">the thread name prefix</param>
        /// <returns>An executor</returns>
        /// <exception cref="ArgumentNullException"><paramref name="namePrefix"/> was null</exception>
        [NotNull] public IExecutor CreateTimeStampCalibratingExecutor([NotNull] string namePrefix) => throw new NotImplementedException();//new TimeStampCalibratingExecutor()
    }
}