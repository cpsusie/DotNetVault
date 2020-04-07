using System;
using System.Threading;
using DotNetVault.ClortonGame;
using HpTimesStamps;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    public class ClortonGameTest 
    {
        public ITestOutputHelper Helper { get; }

        public ClortonGameTest([NotNull] ITestOutputHelper helper) => Helper =
            helper;

        [Fact]
        public void TestBasicClortonGame()
        {
            using (var outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance())
            {
                try
                {
                    if (TimeStampSource.NeedsCalibration)
                    {
                        TimeStampSource.Calibrate();
                    }
                    var clortonFactories = new ClortonGameFactorySource();
                    TimeSpan maxWait = TimeSpan.FromMinutes(2);
                    using var clortonGame = clortonFactories.BasicVaultGameFactory.CreateClortonGame(outputHelper, 3);
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
                catch (Exception ex)
                {
                    Helper.WriteLine("Exception faulted game: [{0}].", ex);
                    throw;
                }
                finally
                {
                    try
                    {
                        string toWrite = outputHelper.GetCurrentTextAndClearBuffer(TimeSpan.FromSeconds(1));
                        Helper.WriteLine("Writing game log:");
                        Helper.WriteLine(toWrite);
                    }
                    catch (Exception ex)
                    {
                        Helper.WriteLine("Fault while trying to retrieve log: [{0}].", ex);
                    }
                }
            }
        }

        [Fact]
        public void TestCustomClortonGame()
        {
            using (var outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance())
            {
                try
                {
                    if (TimeStampSource.NeedsCalibration)
                    {
                        TimeStampSource.Calibrate();
                    }
                    var clortonFactories = new ClortonGameFactorySource();
                    TimeSpan maxWait = TimeSpan.FromMinutes(2);
                    using var clortonGame = clortonFactories.CustomVaultGameFactory.CreateClortonGame(outputHelper, 3);
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
                catch (Exception ex)
                {
                    Helper.WriteLine("Exception faulted game: [{0}].", ex);
                    throw;
                }
                finally
                {
                    try
                    {
                        string toWrite = outputHelper.GetCurrentTextAndClearBuffer(TimeSpan.FromSeconds(1));
                        Helper.WriteLine("Writing game log:");
                        Helper.WriteLine(toWrite);
                    }
                    catch (Exception ex)
                    {
                        Helper.WriteLine("Fault while trying to retrieve log: [{0}].", ex);
                    }
                }
            }
        }

        static ClortonGameTest()
        {
            CgTimeStampSource.SupplyAlternateProvider(new HpTsProvider());
        }

        private void ClortonGame_GameEnded(object sender, ClortonGameEndedEventArgs e)
        {
            if (e == null) throw new Exception();
            Interlocked.CompareExchange(ref _endedEventArgs, e, null);
        }


        private volatile ClortonGameEndedEventArgs _endedEventArgs;
    }

    internal sealed class OutputHelperWrapper : ITestOutputHelper, IDisposableOutputHelper
    {
        public static OutputHelperWrapper CreateWrapper([NotNull] ITestOutputHelper helper) =>
            new OutputHelperWrapper(helper ?? throw new ArgumentNullException(nameof(helper)));

        public bool IsDisposed { get; private set; }

        public void WriteLine(string message) =>
            _helper.WriteLine(message);
        

        public void WriteLine(string format, params object[] args)
            =>  _helper.WriteLine(format, args);
        

        //do nothing
        public void Dispose() =>
            IsDisposed = true;

        private OutputHelperWrapper([NotNull] ITestOutputHelper helper) => _helper = helper;

        [NotNull] private readonly ITestOutputHelper _helper;
    }

    internal sealed class HpTsProvider : TimeStampProvider
    {
        public override DateTime Now => TimeStampSource.Now;

        public override void Calibrate() => TimeStampSource.Calibrate();
    }
}
