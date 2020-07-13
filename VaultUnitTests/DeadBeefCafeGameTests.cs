using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using DotNetVault.ClortonGame;
using DotNetVault.DeadBeefCafeBabeGame;
using HpTimesStamps;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using System.Linq.Expressions;
using DotNetVault.ExtensionMethods;

namespace VaultUnitTests
{
    public class DeadBeefCafeBabeGameTests
    {
        public ITestOutputHelper Helper { get; }

        public DeadBeefCafeBabeGameTests([NotNull] ITestOutputHelper helper) => Helper =
            helper;

        [Fact]
        public void TestGame()
        {
            
            using (var outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance())
            {
                string gameLog = null;
                try
                {
                    if (TimeStampSource.NeedsCalibration)
                    {
                        TimeStampSource.Calibrate();
                    }

                    IDeadBeefCafeGameFactory cafeBabeFactory = new DeadBeefCafeGameFactorySource().FactoryInstance;

                    TimeSpan maxWait = TimeSpan.FromSeconds(2);
                    using var cafeBabeGame =
                        cafeBabeFactory.CreateDeadBeefCafeGame(outputHelper, 3, CafeBabeGame_GameEnded);
                    DateTime startedAt = TimeStampSource.Now;
                    DateTime quitAfter = startedAt + maxWait;

                    while (!_endedEventArgs.IsSet && !cafeBabeGame.IsCancelled && TimeStampSource.Now <= quitAfter)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(10));
                        Helper.WriteLine("{0:F3} milliseconds elapsed since the clorton game began.",
                            (TimeStampSource.Now - startedAt).TotalMilliseconds);
                    }

                    var endedEventArgs = _endedEventArgs.Value;
                    if (endedEventArgs != null)
                    {
                        
                        Helper.WriteLine("Received ended event args.");
                        Helper.WriteLine("Content: [" + endedEventArgs + "].");
                        Assert.True(endedEventArgs.Results.Success &&
                                    endedEventArgs.Results.NumberFoundAtIndex != null &&
                                    endedEventArgs.Results.FinalArray
                                        [endedEventArgs.Results.NumberFoundAtIndex.Value] ==
                                    cafeBabeGame.LookForNumber);
                        Helper.WriteLine("Array printout: ");
                        Helper.WriteLine(endedEventArgs.ArrayText);
                        Helper.WriteLine(string.Empty);
                       
                    }
                    else
                    {
                        Helper.WriteLine("Dead beef cafe babe game did not succeed.");
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
                        gameLog = toWrite;
                    }
                    catch (Exception ex)
                    {
                        Helper.WriteLine("Fault while trying to retrieve log: [{0}].", ex);
                    }
                }

                if (gameLog != null)
                {
                    Assert.True(ValidateLog(gameLog));
                }
            }
        }

        static bool ValidateLog(string gameLog)
        {

            if (string.IsNullOrWhiteSpace(gameLog))
            {
                return false;
            }
            var arr = gameLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            const string upTo = "0xDEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE";
            const string xVal =
                "0xC0DE_D00D_FEA2_B00B_C0DE_D00D_FEA2_B00B_C0DE_D00D_FEA2_B00B_C0DE_D00D_FEA2_B00B";
            const string oVal =
                "0xFACE_C0CA_F00D_BAD0_FACE_C0CA_F00D_BAD0_FACE_C0CA_F00D_BAD0_FACE_C0CA_F00D_BAD0";

            int xCount = 0;
            int oCount = 0;
            var strings = (from str in arr
                           where str?.StartsWith("Logged at") == true && (str.Contains(upTo, StringComparison.OrdinalIgnoreCase) || str.Contains(xVal, StringComparison.OrdinalIgnoreCase) || str.Contains(oVal, StringComparison.OrdinalIgnoreCase))
                           select str.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();

            int indexOfDeadBeef = -1;
            foreach (var item in strings.EnumerateWithIndices())
            {
                if (item.Val.Length == 10)
                {
                    if (string.Equals(item.Val[6].Trim(), upTo, StringComparison.OrdinalIgnoreCase))
                        indexOfDeadBeef = item.Index;
                    break;
                }
            }


            
            if (indexOfDeadBeef < 0 || indexOfDeadBeef >= strings.Length)
            {
                return false;
            }

            var slice = strings.AsSpan().Slice(0, indexOfDeadBeef);
            foreach (var str in slice)
            {
                if (str.Length < 9)
                    return false;

                bool isEx = string.Equals(str[4].Trim(), xVal, StringComparison.OrdinalIgnoreCase);
                bool isO = string.Equals(str[4].Trim(), oVal, StringComparison.OrdinalIgnoreCase);

                if (isO == isEx)
                {
                    return false;
                }

                bool parsedCount = int.TryParse(str[7], out int s);
                if (!parsedCount)
                {
                    return false;
                }

                if (isEx)
                {
                    xCount += s;
                }
                else
                {
                    oCount += s;
                }

            }


            int difference = xCount - oCount;
            if (difference < 0)
            {
                difference = 0 - difference;
            }

            return difference % 13 == 0;


        }

        private void CafeBabeGame_GameEnded(object sender, DeadBeefCafeGameEndedEventArgs e)
        {
            if (e != null)
            {
                _endedEventArgs.SetOrThrow(e);
            }
        }

        static DeadBeefCafeBabeGameTests()
        {
            CgTimeStampSource.SupplyAlternateProvider(new HpTsProvider());
        }

        private LocklessSetOnce<DeadBeefCafeGameEndedEventArgs> _endedEventArgs = default;
    }

    internal struct LocklessSetOnce<T> where T : class
    {
        public bool IsSet
        {
            get
            {
                T val = _value;
                return val != null;
            }
        }

        [CanBeNull]
        public T Value
        {
            get
            {
                T ret = _value;
                return ret;
            }
        }

        public bool TrySet([NotNull] T value)
        {
            return Interlocked.CompareExchange(ref _value,
                value ?? throw new ArgumentNullException(nameof(value)), null) == null;

        }

        public void SetOrThrow([NotNull] T value)
        {
            if (!TrySet(value)) throw new InvalidOperationException("Already set.");
        }

        [CanBeNull] private volatile T _value;
    }
}
