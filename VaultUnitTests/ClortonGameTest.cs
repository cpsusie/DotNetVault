using System;
using System.Threading;
using HpTimesStamps;
using JetBrains.Annotations;
using VaultUnitTests.ClortonGame;
using Xunit;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    public class ClortonGameTest : IDisposable
    {
        public IDisposableOutputHelper Helper { get; }

        public ClortonGameTest([NotNull] ITestOutputHelper helper) => Helper = ThreadSafeTestOutputHelper.CreateOutputHelper(helper ?? throw new ArgumentNullException(nameof(helper)));

        [Fact]
        public void TestClortonGame()
        {
            try
            {
                if (TimeStampSource.NeedsCalibration)
                {
                    TimeStampSource.Calibrate();
                }

                TimeSpan maxWait = TimeSpan.FromMinutes(2);
                using var clortonGame = ClortonGame.ClortonGame.CreateClortonGame(Helper, 3);
                clortonGame.GameEnded += ClortonGame_GameEnded;
                DateTime startedAt = TimeStampSource.Now;
                DateTime quitAfter = startedAt + maxWait;

                while (_endedEventArgs == null && !clortonGame.IsCancelled && TimeStampSource.Now <= quitAfter)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    Helper.WriteLine("{0:F3} milliseconds elapsed since the clorton game began.",
                        (TimeStampSource.Now - startedAt).TotalMilliseconds);
                }

                if (_endedEventArgs != null)
                {
                    Helper.WriteLine("Received ended event args.");
                    Helper.WriteLine("Content: [" + _endedEventArgs + "].");
                    Assert.True(_endedEventArgs.Results.Success);
                }
                else
                {
                    Helper.WriteLine("Clorton game did not succeed.");
                    Assert.False(true);
                }
            }
            finally
            {
                Helper.Dispose();
            }
            
        }

        private void ClortonGame_GameEnded(object sender, ClortonGameEndedEventArgs e)
        {
            if (e == null) throw new Exception();
            Interlocked.CompareExchange(ref _endedEventArgs, e, null);
        }

        public void Dispose() => Helper.Dispose();

        ~ClortonGameTest() => Helper?.Dispose();

        private volatile ClortonGameEndedEventArgs _endedEventArgs = null;
    }
}
