using System;
using DotNetVault.ClortonGame;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Abstract base class for the dead beef cafe game demonstrating usage of and providing stress test for
    /// the <see cref="ReadWriteValueListVault{TItem}"/> vault.
    /// </summary>
    public abstract class DeadBeefCafeBabeGameBase : IDeadBeefCafeGame
    {
        #region Properties and Events
        /// <summary>
        /// The game constants
        /// </summary>
        public static DeadBeefCafeBabeGameConstants GameConstants { get; } = new DeadBeefCafeBabeGameConstants();
        /// <inheritdoc />
        public ref readonly UInt256 LookForNumber => ref GameConstants.LookForNumber;
        /// <inheritdoc />
        public ref readonly UInt256 XNumber => ref GameConstants.XNumber;
        /// <inheritdoc />
        public ref readonly UInt256 ONumber => ref GameConstants.ONumber;
        /// <inheritdoc />
        public abstract bool IsDisposed { get; }
        /// <inheritdoc />
        public abstract bool StartEverRequested { get; }
        /// <inheritdoc />
        public abstract bool EverStarted { get; }
        /// <inheritdoc />
        public abstract bool IsCancelled { get; }
        /// <inheritdoc />
        public abstract int PendingReaderThreads { get; }
        /// <inheritdoc />
        public event EventHandler<DeadBeefCafeGameEndedEventArgs> GameEnded;

        /// <summary>
        /// The concrete type of this object (most derived)
        /// </summary>
        [NotNull] protected Type ConcreteType => _concreteType.Value;
        /// <summary>
        /// The text representation of the concrete type of this object (most derived)
        /// </summary>
        [NotNull] protected string ConcreteTypeName => ConcreteType.Name;
        #endregion

        #region CTOR
        private protected DeadBeefCafeBabeGameBase() => _concreteType = new LocklessConcreteType(this);
        #endregion

        #region Public Methods
        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        } 
        #endregion

        #region abstract methods
        /// <summary>
        /// Initialize the vault
        /// </summary>
        /// <returns>the vault.</returns>
        [NotNull] protected abstract ReadWriteValueListVault<UInt256> InitTVault();

        /// <summary>
        /// Initialize the arbiter thread
        /// </summary>
        /// <param name="vault">the vault</param>
        /// <param name="outputHelper">the output helper</param>
        /// <returns>the arbiter thread</returns>
        [NotNull]
        protected abstract ArbiterThread InitArbiterThread([NotNull] ReadWriteValueListVault<UInt256> vault,
            [NotNull] IOutputHelper outputHelper);
        /// <summary>
        /// Initialize a writer thread
        /// </summary>
        /// <param name="vault">the vault</param>
        /// <param name="outputHelper"></param>
        /// <param name="favoriteNumber"></param>
        /// <param name="beginToken"></param>
        /// <returns></returns>
        [NotNull]
        protected abstract WriterThread InitWriterThread([NotNull] ReadWriteValueListVault<UInt256> vault,
            [NotNull] IOutputHelper outputHelper, in UInt256 favoriteNumber, [NotNull] WriterThreadBeginToken beginToken);
        /// <summary>
        /// Initialize a reader thread
        /// </summary>
        /// <param name="vault">the vault</param>
        /// <param name="outputHelper">the io helper</param>
        /// <param name="index">the index of the thread in reader thread array</param>
        /// <returns>a reader thread</returns>
        [NotNull]
        protected abstract ReaderThread InitReaderThread([NotNull] ReadWriteValueListVault<UInt256> vault,
            [NotNull] IOutputHelper outputHelper, int index);
        #endregion

        #region Virtual Methods
        /// <summary>
        /// dispose
        /// </summary>
        /// <param name="disposing">true if called by code,
        /// false if by gc finalizer</param>
        protected virtual void Dispose(bool disposing) => GameEnded = null;

        /// <summary>
        /// game ended invocator
        /// </summary>
        /// <param name="e">the event args</param>
        protected virtual void OnGameEnded([NotNull] DeadBeefCafeGameEndedEventArgs e)
            => GameEnded?.Invoke(this, e); 
        #endregion

        #region MyRegion
        [NotNull] private readonly LocklessConcreteType _concreteType;
        #endregion
    }
}