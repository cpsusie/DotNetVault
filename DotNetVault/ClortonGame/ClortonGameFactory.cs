using System;
using System.Text;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// Provides factories to create clorton games
    /// </summary>
    public readonly partial struct ClortonGameFactorySource
    {
        /// <summary>
        /// Factory used to create a clorton game using the <see cref="BasicReadWriteVault{T}"/> vault.
        /// </summary>
        [NotNull]
        public IClortonGameFactory BasicVaultGameFactory => TheBasicFactory;

        /// <summary>
        /// Factory used to create a clorton game using the <see cref="ReadWriteStringBufferVault"/> vault.
        /// </summary>
        [NotNull] public IClortonGameFactory CustomVaultGameFactory => TheCustomFactory;


        private static readonly IClortonGameFactory
            TheCustomFactory = CustomVaultClortonGameFactory.CreateGameFactory();
        private static readonly IClortonGameFactory TheBasicFactory = BasicVaultClortonGameFactory.CreateGameFactory();
    }

    readonly partial struct ClortonGameFactorySource
    {

        private sealed class CustomVaultClortonGameFactory : IClortonGameFactory
        {
            internal static IClortonGameFactory CreateGameFactory() => new CustomVaultClortonGameFactory();

            public IClortonGame CreateClortonGame(IDisposableOutputHelper helper, int numReader) =>
                CustomVaultClortonGame.CreateCustomVaultClortonGame(helper, numReader);

            public IClortonGame CreateClortonGame(IDisposableOutputHelper helper, int numReader,
                EventHandler<ClortonGameEndedEventArgs> doneHandler) =>
                CustomVaultClortonGame.CreateCustomVaultClortonGame(helper, numReader, doneHandler);

            private CustomVaultClortonGameFactory() { }
        }

        private sealed class BasicVaultClortonGameFactory : IClortonGameFactory
        {
            internal static IClortonGameFactory CreateGameFactory() => new BasicVaultClortonGameFactory();

            public IClortonGame CreateClortonGame(IDisposableOutputHelper helper, int numReader) =>
                BasicVaultClortonGame.CreateBasicVaultClortonGame(helper, numReader);

            public IClortonGame CreateClortonGame(IDisposableOutputHelper helper, int numReader,
                EventHandler<ClortonGameEndedEventArgs> doneHandler) =>
                BasicVaultClortonGame.CreateBasicVaultClortonGame(helper, numReader, doneHandler);

            private BasicVaultClortonGameFactory() { }
        }

        private sealed class BasicVaultClortonGame : ClortonGameBase<BasicReadWriteVault<string>>
        {
            internal static IClortonGame CreateBasicVaultClortonGame([NotNull] IDisposableOutputHelper outputHelper,
                int numReaders, [CanBeNull] EventHandler<ClortonGameEndedEventArgs> doneHandler)
            {
                if (outputHelper == null) throw new ArgumentNullException(nameof(outputHelper));
                if (numReaders < 2)
                    throw new ArgumentOutOfRangeException(nameof(numReaders), numReaders,
                        @"At least two readers required.");
                Func<ClortonGameBase<BasicReadWriteVault<string>>> ctor = () =>
                    new BasicVaultClortonGame(outputHelper, numReaders);
                return CreateClortonGame(ctor, outputHelper, doneHandler);
            }

            internal static IClortonGame CreateBasicVaultClortonGame([NotNull] IDisposableOutputHelper outputHelper,
                int numReaders) => CreateBasicVaultClortonGame(outputHelper, numReaders, null);

            private BasicVaultClortonGame([NotNull] IDisposableOutputHelper outputHelper,
                int numReaders) : base(outputHelper, numReaders)
            {
            }

            private protected override BasicReadWriteVault<string> InitTVault() =>
                new BasicReadWriteVault<string>(string.Empty, TimeSpan.FromMilliseconds(250));

            private protected override ArbiterThread<BasicReadWriteVault<string>> InitArbiterThread(
                BasicReadWriteVault<string> vault, IOutputHelper outputHelper)
                => new BasicVaultArbiterThread(vault, outputHelper);

            private protected override WriterThread<BasicReadWriteVault<string>> InitWriterThread(
                BasicReadWriteVault<string> vault, IOutputHelper outputHelper, char charToWrite,
                WriterThreadBeginToken beginToken) =>
                new BasicVaultWriterThread(vault, outputHelper, charToWrite, beginToken);

            private protected override ReaderThread<BasicReadWriteVault<string>> InitReaderThread(
                BasicReadWriteVault<string> vault, IOutputHelper outputHelper, int index, string lookFor)
                => new BasicVaultReaderThread(vault, outputHelper, index, lookFor);
        }

        private sealed class CustomVaultClortonGame : ClortonGameBase<ReadWriteStringBufferVault>
        {
            internal static IClortonGame CreateCustomVaultClortonGame([NotNull] IDisposableOutputHelper outputHelper,
                int numReaders, [CanBeNull] EventHandler<ClortonGameEndedEventArgs> doneHandler)
            {
                if (outputHelper == null) throw new ArgumentNullException(nameof(outputHelper));
                if (numReaders < 2)
                    throw new ArgumentOutOfRangeException(nameof(numReaders), numReaders,
                        @"At least two readers required.");
                Func<ClortonGameBase<ReadWriteStringBufferVault>> ctor = () =>
                    new CustomVaultClortonGame(outputHelper, numReaders);
                return CreateClortonGame(ctor, outputHelper, doneHandler);
            }

            internal static IClortonGame CreateCustomVaultClortonGame([NotNull] IDisposableOutputHelper outputHelper,
                int numReaders) => CreateCustomVaultClortonGame(outputHelper, numReaders, null);

            private CustomVaultClortonGame([NotNull] IDisposableOutputHelper outputHelper, int numReaders) 
                : base(outputHelper, numReaders) { }

            private protected override ArbiterThread<ReadWriteStringBufferVault> InitArbiterThread(
                ReadWriteStringBufferVault vault, IOutputHelper outputHelper) =>
                new StringBufferVaultArbiterThread(vault, outputHelper);
            private protected override ReaderThread<ReadWriteStringBufferVault> InitReaderThread(
                ReadWriteStringBufferVault vault, IOutputHelper outputHelper, int index,
                string lookFor) => new CustomVaultReaderThread(vault, outputHelper, index, lookFor);
            private protected override ReadWriteStringBufferVault InitTVault() =>
                new ReadWriteStringBufferVault(TimeSpan.FromMilliseconds(250),
                    () => new StringBuilder(BufferStartingSize));
            private protected override WriterThread<ReadWriteStringBufferVault> InitWriterThread(
                ReadWriteStringBufferVault vault, IOutputHelper outputHelper, char charToWrite,
                WriterThreadBeginToken beginToken) =>
                new CustomVaultWriterThread(vault, outputHelper, charToWrite, beginToken);
           

            private const int BufferStartingSize = 64;
        }
    }
}
