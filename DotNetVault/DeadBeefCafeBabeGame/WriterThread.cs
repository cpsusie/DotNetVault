using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DotNetVault.ClortonGame;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// A writer thread used in the dead beef cafe babe game.
    /// </summary>
    public sealed class WriterThread : CafeBabeGameThread
    {
        /// <summary>
        /// Default value for <see cref="MinWrites"/>
        /// </summary>
        public const int DefaultMinWrites = 0;
        /// <summary>
        /// Default value for <see cref="MaxWrites"/>
        /// </summary>
        public const int DefaultMaxWrites = 5;
        /// <summary>
        /// Minimum number of times it writes its <see cref="FavoriteNumber"/> on
        /// any given access.
        /// </summary>
        public int MinWrites { get; }
        /// <summary>
        /// Maximum number of times it writes its <see cref="FavoriteNumber"/> on
        /// any given access.
        /// </summary>
        public int MaxWrites { get; }
        /// <summary>
        /// The number it writes
        /// </summary>
        public ref readonly UInt256 FavoriteNumber => ref _number;

        /// <summary>
        ///CTOR
        /// </summary>
        /// <param name="vault">vault protecting the string</param>
        /// <param name="helper">helper for i/o</param>
        /// <param name="favoriteNumber">number it writes</param>
        /// <param name="token">Start token</param>
        /// <param name="minWrites">min writes</param>
        /// <param name="maxWrites">max writes</param>
        /// <exception cref="ArgumentNullException"><paramref name="vault"/> or <paramref name="helper"/>
        /// or <paramref name="token"/> was <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Min writes was negative.  Or max writes did not exceed min writes
        /// by at least three.</exception>
        internal WriterThread([NotNull] ReadWriteValueListVault<UInt256> vault, [NotNull] IOutputHelper helper,
            in UInt256 favoriteNumber, [NotNull] WriterThreadBeginToken token, int minWrites = DefaultMinWrites,
            int maxWrites = DefaultMaxWrites) : base(vault, helper)
        {
            if (minWrites < 0)
                throw new ArgumentOutOfRangeException(nameof(minWrites), minWrites, @"Parameter cannot be negative.");
            if (maxWrites <= minWrites + 2)
                throw new ArgumentOutOfRangeException(nameof(maxWrites), maxWrites,
                    $@"Parameter must be greater than {(minWrites + 2)}");
            _number = favoriteNumber;
            _startToken = token ?? throw new ArgumentNullException(nameof(token));
            MaxWrites = maxWrites;
            MinWrites = minWrites;
        }

        /// <inheritdoc />
        protected override void ExecuteJob(CancellationToken token)
        {
            while (!_startToken.BeginSignaled)
            {
                token.ThrowIfCancellationRequested();
            }
            while (true)
            {
                try
                {
                    int numVals;
                    {
                        token.ThrowIfCancellationRequested();
                        numVals = RGen.Next(MinWrites, MaxWrites + 1); //random determine number of times to write char
                        if (numVals > 0)
                        {
                            int numWritesCount = 0;
                            //get exclusive write lock
                            using var lck = _valueList.Lock(token);
                            while (numWritesCount++ < numVals) //build the string we are going to append
                            {
                                lck.Add(in FavoriteNumber); //append the favorite number
                            }

                        }//lock released
                    }
                    _helper.WriteLine("Writer of " + FavoriteNumber + " wrote it " + 
                                      numVals + " times.");
                }
                catch (TimeoutException ex)
                {
                    _helper.WriteLine("Writer thread with fav num [{0}], timed out with ex: [{1}].",
                        FavoriteNumber.ToString(), ex);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _helper.WriteLine("Writer thread with fav num [{0}] faulted with ex: [{1}]",
                        FavoriteNumber.ToString(), ex);
                    throw;
                }
            }
        }

        //don't do anything special
        /// <inheritdoc /> 
        protected override void PerformFinishingActions() { }

        /// <inheritdoc />
        protected override Thread InitThread() => new Thread(ThreadLoop)
            { IsBackground = true, Name = $"Writer_{FavoriteNumber.ToString()}" };

        #region Privates  (don't touch)
        [NotNull] private Random RGen => TheRGen.Value;
        private static readonly ThreadLocal<Random> TheRGen =
            new ThreadLocal<Random>(() => new Random());
        [NotNull] private readonly WriterThreadBeginToken _startToken;
        private readonly UInt256 _number; 
        #endregion
    }
}
