using System;
using System.Threading;
using DotNetVault.Vaults;
using HpTimesStamps;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace VaultUnitTests.ClortonGame
{
    sealed class WriterThread : ClortonGameThread
    {
        public const int DefaultMinWrites = 0;
        public const int DefaultMaxWrites = 5;
        
        public char Char { get; }
        public int MinWrites { get; }
        public int MaxWrites { get; }

        public WriterThread([NotNull] BasicReadWriteVault<string> vault, [NotNull] ITestOutputHelper helper,
            char favoriteChar, int minWrites = DefaultMinWrites, int maxWrites = DefaultMaxWrites) : base(vault, helper)
        {
            if (minWrites < 0)
                throw new ArgumentOutOfRangeException(nameof(minWrites), minWrites, @"Parameter cannot be negative.");
            if (maxWrites <= minWrites + 2)
                throw new ArgumentOutOfRangeException(nameof(maxWrites), maxWrites,
                    $@"Parameter must be greater than {(minWrites + 2).ToString()}");
            Char = favoriteChar;
            MaxWrites = maxWrites;
            MinWrites = minWrites;
        }

        protected override void ExecuteJob(CancellationToken token)
        {
            while (true)
            {
                try
                {
                    DateTime writeTs;
                    int numVals;
                    {
                        token.ThrowIfCancellationRequested();
                        using var lck = _vault.Lock(token);
                        numVals = RGen.Next(MinWrites, MaxWrites + 1);
                        int numWritesCount = 0;
                        while (numWritesCount++ < numVals)
                        {
                            lck.Value += Char;
                        }
                        writeTs = TimeStampSource.Now;
                    }
                    _helper.WriteLine("At [" + writeTs.ToString("O") + "], writer of " + Char + " wrote " + Char + " " +
                                      numVals + " times.");
                }
                catch (TimeoutException ex)
                {
                    DateTime ts = TimeStampSource.Now;
                    _helper.WriteLine("At [{0:O}], writer thread with char [{1}], timed out with ex: [{2}].",
                        ts, Char.ToString(), ex);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    DateTime ts = TimeStampSource.Now;
                    _helper.WriteLine("At [{0:O}], writer thread with char [{1}] faulted with ex: [{2}]", ts,
                        Char.ToString(), ex);
                    throw;
                }
            }
        }

        protected override void PerformFinishingActions()
        {
            
        }

        protected override Thread InitThread() => new Thread(ThreadLoop)
            {IsBackground = true, Name = $"Writer_{Char.ToString()}"};

        [NotNull] private Random RGen => TheRGen.Value;
        [NotNull] private static readonly ThreadLocal<Random> TheRGen = new ThreadLocal<Random>(() => new Random());
    }
}