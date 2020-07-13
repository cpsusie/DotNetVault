using System;
using DotNetVault.ClortonGame;
using DotNetVault.Logging;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Source for obtaining access to factory.
    /// </summary>
    public readonly partial struct DeadBeefCafeGameFactorySource
    {

        /// <summary>
        /// True if <see cref="FactoryInstance"/> has been initialized at all (either to default or alternate impl)
        /// </summary>
        public bool IsInitialized => TheFactory.HasValue && TheFactory.Value != null;

        /// <summary>
        /// True if <see cref="FactoryInstance"/> has been default initialized.
        /// </summary>
        public bool IsDefaultInitialized =>
            TheFactory.HasValue && TheFactory.Value is DeadBeefCafeBabeGameFactoryDefImpl;

        /// <summary>
        /// Access to factory instance
        /// </summary>
        public IDeadBeefCafeGameFactory FactoryInstance => TheFactory.Value;

        /// <summary>
        /// Supply a different factory -- can only be called once and only before first access
        /// to <see cref="FactoryInstance"/>.
        /// </summary>
        /// <param name="alternate">the alternate factory</param>
        /// <returns>true if the factory used was replaced with alternate, false if already set or default initialized.</returns>
        public static bool SupplyAlternateFactory([NotNull] IDeadBeefCafeGameFactory alternate) =>
            TheFactory.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        static DeadBeefCafeGameFactorySource() => TheFactory = new LocklessWriteOnce<IDeadBeefCafeGameFactory>(DeadBeefCafeBabeGameFactoryDefImpl.CreateFactoryInstance);
        [NotNull] private static readonly LocklessWriteOnce<IDeadBeefCafeGameFactory> TheFactory;
    }

    #region Nested type definitions
    readonly partial struct DeadBeefCafeGameFactorySource
    {
        private sealed class DeadBeefCafeBabeGameFactoryDefImpl : IDeadBeefCafeGameFactory
        {
            internal static IDeadBeefCafeGameFactory CreateFactoryInstance() => new DeadBeefCafeBabeGameFactoryDefImpl();

            /// <inheritdoc />
            public IDeadBeefCafeGame CreateDeadBeefCafeGame(IDisposableOutputHelper helper, int numReader) =>
                CreateDeadBeefCafeGame(helper, numReader, null);

            /// <inheritdoc />
            public IDeadBeefCafeGame CreateDeadBeefCafeGame(IDisposableOutputHelper helper, int numReader,
                EventHandler<DeadBeefCafeGameEndedEventArgs> doneHandler) =>
                DeadBeefCafeGame.CreateDeadBeefCafeGame(helper, numReader, doneHandler);

            private DeadBeefCafeBabeGameFactoryDefImpl() { }

        }
    } 
    #endregion
}