using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Interfaces;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using TimeStampSource = DotNetVault.ClortonGame.CgTimeStampSource;
namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// A writer thread configured to use <see cref="ReadWriteStringBufferVault"/>
    /// </summary>
    public sealed class CustomVaultWriterThread : WriterThread<ReadWriteStringBufferVault>
    {
        /// <inheritdoc />
        public CustomVaultWriterThread([NotNull] ReadWriteStringBufferVault vault, [NotNull] IOutputHelper helper, char favoriteChar, WriterThreadBeginToken startToken, int minWrites = DefaultMinWrites, int maxWrites = DefaultMaxWrites) : base(vault, helper, favoriteChar, startToken, minWrites, maxWrites)
        {
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
                        string appendMe = string.Empty;
                        token.ThrowIfCancellationRequested();
                        numVals = RGen.Next(MinWrites, MaxWrites + 1); //random determine number of times to write char
                        int numWritesCount = 0;
                        while (numWritesCount++ < numVals) //build the string we are going to append
                        {
                            appendMe += Char;
                        }
                        //Get exclusive write lock
                        using var lck = _vault.Lock(token);
                        lck.Append(appendMe); //append
                                              //the value
                    }
                    _helper.WriteLine("Writer of " + Char + " wrote " + Char + " " +
                                      numVals + " times.");
                }
                catch (TimeoutException ex)
                {
                    _helper.WriteLine("Writer thread with char [{0}], timed out with ex: [{1}].",
                        Char.ToString(), ex);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _helper.WriteLine("Writer thread with char [{0}] faulted with ex: [{1}]",
                        Char.ToString(), ex);
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// A writer thread configured to use <see cref="BasicReadWriteVault{T}"/>.
    ///  </summary>
    public sealed class BasicVaultWriterThread : WriterThread<BasicReadWriteVault<string>>
    {
        /// <inheritdoc />
        public BasicVaultWriterThread([NotNull] BasicReadWriteVault<string> vault, [NotNull] IOutputHelper helper, char favoriteChar, WriterThreadBeginToken startToken, int minWrites = DefaultMinWrites, int maxWrites = DefaultMaxWrites) : base(vault, helper, favoriteChar, startToken, minWrites, maxWrites)
        {
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
                        string appendMe = string.Empty;
                        token.ThrowIfCancellationRequested();
                        numVals = RGen.Next(MinWrites, MaxWrites + 1); //random determine number of times to write char
                        int numWritesCount = 0;
                        while (numWritesCount++ < numVals) //build the string we are going to append
                        {
                            appendMe += Char;
                        }
                        //Get exclusive write lock
                        using var lck = _vault.Lock(token);
                        lck.Value += appendMe; //append the value
                    }
                    _helper.WriteLine("Writer of " + Char + " wrote " + Char + " " +
                                      numVals + " times.");
                }
                catch (TimeoutException ex)
                {
                    _helper.WriteLine("Writer thread with char [{0}], timed out with ex: [{1}].",
                        Char.ToString(), ex);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _helper.WriteLine("Writer thread with char [{0}] faulted with ex: [{1}]",
                        Char.ToString(), ex);
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// These threads demonstrate use of exclusive read-write lock.  They obtain
    /// an exclusive right lock then append a random number between <see cref="MinWrites"/>
    /// and <see cref="MaxWrites"/> (inclusive)
    /// of its <see cref="Char"/>.  They then release the lock.  Rinse and Repeat
    /// </summary>
    public abstract class WriterThread<TVault> : ClortonGameThread<TVault> where TVault : IBasicVault<string>
    {
        #region Public properties and constants

      
        /// <summary>
        /// Default value for <see cref="MinWrites"/>
        /// </summary>
        public const int DefaultMinWrites = 0;
        /// <summary>
        /// Default value for <see cref="MaxWrites"/>
        /// </summary>
        public const int DefaultMaxWrites = 5;

        /// <summary>
        /// The char it writes
        /// </summary>
        public char Char { get; }
        /// <summary>
        /// Minimum number of times it writes its <see cref="Char"/> on
        /// any given access.
        /// </summary>
        public int MinWrites { get; }
        /// <summary>
        /// Maximum number of times it writes its <see cref="Char"/> on
        /// any given access.
        /// </summary>
        public int MaxWrites { get; } 
        #endregion

        #region CTOR

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="vault">vault protecting the string</param>
        /// <param name="helper">helper for i/o</param>
        /// <param name="favoriteChar">char it writes</param>
        /// <param name="startToken">Start token</param>
        /// <param name="minWrites">min writes</param>
        /// <param name="maxWrites">max writes</param>
        /// <exception cref="ArgumentNullException"><paramref name="vault"/> or <paramref name="helper"/>
        /// were <see langword="null"/> </exception>
        /// <exception cref="ArgumentOutOfRangeException">Min writes was negative.  Or max writes did not exceed min writes
        /// by at least three.</exception>
        protected WriterThread([NotNull] TVault vault, [NotNull] IOutputHelper helper,
            char favoriteChar, WriterThreadBeginToken startToken, int minWrites = DefaultMinWrites, int maxWrites = DefaultMaxWrites) : base(vault, helper)
        {
            if (minWrites < 0)
                throw new ArgumentOutOfRangeException(nameof(minWrites), minWrites, @"Parameter cannot be negative.");
            if (maxWrites <= minWrites + 2)
                throw new ArgumentOutOfRangeException(nameof(maxWrites), maxWrites,
                    $@"Parameter must be greater than {(minWrites + 2).ToString()}");
            Char = favoriteChar;
            MaxWrites = maxWrites;
            MinWrites = minWrites;
            _startToken = startToken ?? throw new ArgumentNullException(nameof(startToken));
        } 
        #endregion

        #region Methods


        /// <inheritdoc />
        protected sealed override void Dispose(bool disposing)
        {
            //if (disposing)
            //{
            //    StackTrace st = new StackTrace();
            //    _disposeStackTrace = st.ToString();
            //}
            base.Dispose(disposing);

        }

        //don't do anything special
        /// <inheritdoc /> 
        protected sealed override void PerformFinishingActions() { }

        /// <inheritdoc />
        protected sealed override Thread InitThread() => new Thread(ThreadLoop)
            { IsBackground = true, Name = $"Writer_{Char.ToString()}" };
        #endregion

        #region Privates (dont touch)
        
        [NotNull] private protected Random RGen => TheRGen.Value;
        // ReSharper disable once StaticMemberInGenericType
        private static readonly ThreadLocal<Random> TheRGen =
            new ThreadLocal<Random>(() => new Random());
        [NotNull] private protected readonly WriterThreadBeginToken _startToken;

        #endregion
    }

    /// <summary>
    /// Make sure writer threads start around same time
    /// </summary>
    public sealed class WriterThreadBeginToken
    {
        /// <summary>
        /// True if begin has been signalled
        /// </summary>
        public bool BeginSignaled
        {
            get
            {
                int begin = _begin;
                return begin == Begin;
            }
        }

        /// <summary>
        /// Signal begin.  Throws if not already begun.
        /// </summary>
        public void SignalBegin()
        {
            if (!TrySignalBegin()) throw new InvalidOperationException("Begin already signaled");
        }

        /// <summary>
        /// Signal begin
        /// </summary>
        /// <returns>true for success (i.e. first call), false for fail (called again later)</returns>
        public bool TrySignalBegin()
        {
            const int wantToBe = Begin;
            const int needToBeNow = NotYet;
            return Interlocked.CompareExchange(ref _begin, wantToBe, needToBeNow) == needToBeNow;
        }

        private volatile int _begin = NotYet;
        private const int Begin = 1;
        private const int NotYet = 0;
    }
}