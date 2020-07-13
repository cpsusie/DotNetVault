using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.ClortonGame;
using DotNetVault.DeadBeefCafeBabeGame;
using DotNetVault.Vaults;
using HpTimesStamps;
using JetBrains.Annotations;

namespace CafeBabeGame
{
    class Program
    {
        private static readonly IDeadBeefCafeGameFactory GameFactory = new DeadBeefCafeGameFactorySource().FactoryInstance;

        static void Main(string[] args)
        {
            CgTimeStampSource.SupplyAlternateProvider(HpTimeStampProvider.CreateInstance());
            TimeStampSource.Calibrate();
            (bool gotFileOk, string errorGettingFile, FileInfo outputFile, int numGames) = GetOutputFile(args);
            Debug.Assert((outputFile != null) == gotFileOk);
            if (gotFileOk)
            {
                if (numGames > 1)
                {
                    try
                    {
                        PlayMultipleCafeBabeGames(numGames, outputFile);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.ToString());
                        Console.Error.WriteLine("Unexpected error initializing game.  Terminating.");
                        Environment.Exit(-1);
                    }
                }
                else
                {
                    try
                    {
                        PlayCafeBabeGame(outputFile);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.ToString());
                        Console.Error.WriteLine("Unexpected error initializing game.  Terminating.");
                        Environment.Exit(-1);
                    }
                }
            }

        }

        private static void PlayMultipleCafeBabeGames(int count, FileInfo outputFile)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Parameter not positive.");
            string failingBufferContents = string.Empty;
            StringBuilder allLog = new StringBuilder();
            for (int i = 1; i <= count; ++i)
            {
                using IBufferBasedOutputHelper outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance();
                {
                    Console.WriteLine($"Starting game {i} of {count}");
                    Result r = default;
                    using (GameFactory.CreateDeadBeefCafeGame(outputHelper, 3, CafeBabeGame_GameEnded))
                    {
                        r = WaitForGameEndOrTimeout(TimeSpan.FromSeconds(3)) ?? default;
                        if (r == default)
                        {
                            Console.Error.WriteLine(
                                $"FAILED: The cafe babe game# {i} of {count} faulted.  No results can be retrieved.");
                            string bufferContents = TryGetBufferContents(TimeSpan.FromSeconds(2), outputHelper);
                            LogContents(i, null, bufferContents, outputFile);
                            return;
                        }

                        bool success = r.GameResult?.Success == true;
                        if (success)
                        {
                            Console.WriteLine($"Game {i} of {count} finished ok.");
                            string buffer = outputHelper.GetCurrentTextAndClearBuffer(TimeSpan.FromSeconds(2));
                            if (!ValidateLog(buffer))
                            {
                                throw new InvalidOperationException("Unable to validate buffer: [" + buffer + "].");
                            }

                            allLog.Append(buffer);
                        }
                        else
                        {
                            Console.WriteLine($"FAULT Game {i} of {count} FAULTED.");
                            if (r.GameResult?.Cancelled == true)
                            {
                                Console.WriteLine("The game was cancelled.");
                            }
                            else
                            {
                                Console.WriteLine("There were {0} xes and {1} oes.", r.GameResult?.XCount ?? -1,
                                    r.GameResult?.XCount ?? -1);
                                Console.WriteLine(r.GameResult?.WinningThreadIndex != null
                                    ? ("The winning reader thread was thread at index " +
                                       r.GameResult?.WinningThreadIndex.Value + ".")
                                    : "There was no winning thread.");
                            }

                            Environment.Exit(-1);
                            return;
                        }
                    }

                    using (var sw = outputFile.CreateText())
                    {
                        sw.Write(allLog);
                    }

                    try
                    {
                        outputFile.Refresh();
                        if (!outputFile.Exists)
                        {
                            throw new FileNotFoundException("Unable to validate output file write.",
                                outputFile.FullName);
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new FileNotFoundException("Unable to validate output file write.", outputFile.FullName,
                            ex);
                    }

                    Console.WriteLine($"Successfully wrote to {outputFile.FullName}.");

                }

                void LogContents(int gameNum, in Result res, string buff, FileInfo of)
                {
                    Console.WriteLine($"Writing final string and logs to file for failed game# {gameNum}.");
                    try
                    {
                        using (var sw = of.CreateText())
                        {
                            sw.WriteLine("The final string was:");
                            sw.WriteLine(res.ArrayText);
                            sw.WriteLine("END FINAL STRING");

                            if (!string.IsNullOrWhiteSpace(buff))
                            {
                                sw.WriteLine();
                                sw.WriteLine("Log follows:");
                                sw.WriteLine(buff);
                                sw.WriteLine("End LOG.");
                            }
                            else
                            {
                                sw.WriteLine();
                                sw.WriteLine("Log unavailable.");
                            }
                        }

                        Console.WriteLine("Successfully wrote to [" + outputFile.FullName + "].");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("There was an error writing to file [" + outputFile.FullName +
                                                "]: contents: [" + ex + "].  File not written.");
                    }
                }

                string TryGetBufferContents(TimeSpan timeout, IBufferBasedOutputHelper helper)
                {
                    string ret;
                    try
                    {
                        ret = helper.GetCurrentTextAndClearBuffer(timeout);
                    }
                    catch (TimeoutException ex)
                    {
                        Console.Error.WriteLine(ex.ToString());
                        string moreToSay =
                            "Unable to retrieve logs from the output helper " +
                            "because of timeout.  No results file can be written.";
                        Console.Error.WriteLine(moreToSay);
                        ret = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.ToString());
                        string moreToSay = "Unable to retrieve logs from the output helper " +
                                           "because of unexpected exception.  No results file can be written.";
                        Console.Error.WriteLine(moreToSay);
                        ret = string.Empty;
                    }

                    return ret;
                }
            }
        }

        static void PlayCafeBabeGame(FileInfo outputFile)
        {
            Result result;
            string bufferContents;
            using IBufferBasedOutputHelper outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance();
            {
                using IDeadBeefCafeGame game =
                    GameFactory.CreateDeadBeefCafeGame(outputHelper, 3, CafeBabeGame_GameEnded);
                Console.WriteLine("Concrete type of CafeBabe Game: [" + game.GetType().Name + "].");
                
                Result? temp = WaitForGameEndOrTimeout(TimeSpan.FromSeconds(3));
                result = temp ?? default;
            }


            try
            {
                bufferContents = outputHelper.GetCurrentTextAndClearBuffer(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine(ex.ToString());
                string moreToSay =
                    "Unable to retrieve logs from the output helper " +
                    "because of timeout.  No results file can be written.";
                Console.Error.WriteLine(moreToSay);
                bufferContents = string.Empty;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                string moreToSay = "Unable to retrieve logs from the output helper " +
                    "because of unexpected exception.  No results file can be written.";
                Console.Error.WriteLine(moreToSay);
                bufferContents = string.Empty;
            }

            if (result == default)
            {
                Console.Error.WriteLine("The cafe babe game faulted.  No results can be retrieved.");
                Environment.Exit(-1);
                return;
            }

            DeadBeefCafeGameResult finalRes = result.GameResult ?? throw new InvalidOperationException();
            Console.WriteLine(finalRes.Success ? "The game was successful!" : "The game FAILED.");
            if (finalRes.Success)
            {
                bool validated = ValidateLog(bufferContents);
                if (validated)
                {
                    Console.WriteLine("The results were validated.");
                }
                else
                {
                    Console.Error.WriteLine("THE RESULTS COULD NOT BE VALIDATED ... POSSIBLE FLAW IN VAULTS!");
                }
            }

            TimeSpan duration = finalRes.EndedAt - finalRes.StartedAt;
            Console.WriteLine("The game lasted {0:F3} milliseconds", duration.TotalMilliseconds);
            if (finalRes.Cancelled)
            {
                Console.WriteLine("The game was cancelled.");
            }
            else
            {
                Console.WriteLine("There were {0} xes and {1} oes.", finalRes.XCount, finalRes.OCount);
                Console.WriteLine(finalRes.WinningThreadIndex != null
                    ? ("The winning reader thread was thread at index " + finalRes.WinningThreadIndex.Value + ".")
                    : "There was no winning thread.");
            }

            Console.WriteLine("Writing final string and logs to file.");
            try
            {
                using (var sw = outputFile.CreateText())
                {
                    sw.WriteLine("The final array was:");
                    sw.WriteLine(result.ArrayText);
                    sw.WriteLine("END FINAL ARRAY");

                    if (!string.IsNullOrWhiteSpace(bufferContents))
                    {
                        sw.WriteLine();
                        sw.WriteLine("Log follows:");
                        sw.WriteLine(bufferContents);
                        sw.WriteLine("End LOG.");
                    }
                    else
                    {
                        sw.WriteLine();
                        sw.WriteLine("Log unavailable.");
                    }
                }
                Console.WriteLine("Successfully wrote to [" + outputFile.FullName + "].");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("There was an error writing to file [" + outputFile.FullName +
                                        "]: contents: [" + ex + "].  File not written.");
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
            int idx = -1;
            foreach (var item in strings)
            {
                ++idx;
                if (item.Length == 10)
                {
                    if (string.Equals(item[6].Trim(), upTo, StringComparison.OrdinalIgnoreCase))
                        indexOfDeadBeef = idx;
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

        private static bool ValidateFinalResult(in DeadBeefCafeGameResult finalRes)
        {
            Console.WriteLine();
            Console.WriteLine("Validating correctness of final result.");
            var finalArray = finalRes.FinalArray;
            int firstIdx = finalArray.IndexOf(DeadBeefCafeBabeGameBase.GameConstants.LookForNumber);
            if (firstIdx < 0)
            {
                Console.Error.WriteLine("UNABLE TO VALIDATE RESULTS.  POSSIBLE FLAW IN VAULT.");
                Console.Error.WriteLine(DeadBeefCafeBabeGameBase.GameConstants.LookForNumber + " not found!");
                return false;
            }
            else
            {
                ReadOnlySpan<UInt256> preCafeBabeSpan = finalArray.AsSpan();
                preCafeBabeSpan = preCafeBabeSpan.Slice(0, firstIdx);
                int xCount = 0;
                int oCount = 0;
                foreach (ref readonly var num in preCafeBabeSpan)
                {
                    if (num == DeadBeefCafeBabeGameBase.GameConstants.XNumber)
                        ++xCount;
                    else if (num == DeadBeefCafeBabeGameBase.GameConstants.ONumber)
                        ++oCount;
                }
               

                Console.WriteLine(
                    $"Before {DeadBeefCafeBabeGameBase.GameConstants.LookForNumber}, there were {xCount} \"x numbers\" and {oCount} \"o numbers\".");
                int diff = xCount - oCount;
                diff = diff < 0 ? -diff : diff;
                Console.WriteLine($"The difference between the two counts is {diff}.");
                bool validated = diff % 13 == 0;
                if (validated)
                {
                    Console.WriteLine(
                        "The difference between the two counts is evenly divisible by thirteen.  VALIDATED.");
                    Console.WriteLine();
                    return true;
                }

                Console.Error.WriteLine(
                    "The difference between the two is NOT evenly divisible by thirteen.  WARNING RESULTS NOT VALIDATED.  POSSIBLE FLAW IN VAULT LOGIC.");
                Console.Error.WriteLine();
                return false;
            }
        }

        private static Result?  WaitForGameEndOrTimeout(TimeSpan maxWait)
        {
            maxWait = (maxWait <= TimeSpan.Zero) ? TimeSpan.FromSeconds(1) : maxWait;
            DateTime quitAfter = TimeStampSource.Now + maxWait;
            Result res = default;
            while (res == default && TimeStampSource.Now <= quitAfter)
            {
                try
                {
                    using var lck = TheResults.Lock();
                    res = lck.Value;
                    lck.Value = default;
                }
                catch (TimeoutException)
                {
                    Console.Error.WriteLineAsync("Attempt to get results lock timed out.");
                }

                if (res == null)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }
            }
            return res;
        }

        private static void CafeBabeGame_GameEnded(object sender, DeadBeefCafeGameEndedEventArgs e)
        {
            if (e != null)
            {
                using var lck = TheResults.Lock();
                if (lck.Value != default) throw new Exception("Duplicate results!");
                if (e.Results != default)
                {
                    lck.Value = e;
                }
            }
        }

        static (bool Success, string ErrorMessage, FileInfo OutputFile, int NumGamges)
            GetOutputFile(string[] args)
        {
            FileInfo outputFile = null;
            string errorInfo = string.Empty;
            int numGames = 1;
            bool gotNumGames = (args?.Length > 1) && int.TryParse(args[1], out numGames);
            numGames = gotNumGames && numGames > 1 ? numGames : 1;
            string fileName = (args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) ? args[0] : TheDefaultFileName;
            try
            {
                outputFile = new FileInfo(fileName);
                if (outputFile.Exists && outputFile.IsReadOnly)
                {
                    errorInfo = "The file [" + outputFile.FullName + "] " +
                                "cannot be used because it is readonly.";
                    outputFile = null;
                }
            }
            catch (Exception ex)
            {
                errorInfo = "There was a problem accessing the specified file (filename: [" + fileName +
                            "]). Exception contents: [" + ex + "].";
            }

            return (outputFile != null, errorInfo, outputFile, numGames);

        }
       
        private static Random RGen => TheRng.Value;

        private static readonly ThreadLocal<Random> TheRng = new ThreadLocal<Random>(() => new Random());
        private const string TheDefaultFileName = "Cafe_Babe_Game_Results.txt";
        private static readonly BasicMonitorVault<Result> TheResults = new BasicMonitorVault<Result>(default, TimeSpan.FromMilliseconds(100));

        [VaultSafe]
        private readonly struct Result : IEquatable<Result>
        {
            public static implicit operator Result(DeadBeefCafeGameEndedEventArgs e)
            {
                if (e == null)
                {
                    return default;
                }
                return new Result(e.Results, e.ArrayText);
            }

            public DeadBeefCafeGameResult? GameResult => _result;
            [NotNull] public string ArrayText => _finalArrayText ?? string.Empty;

            public Result(DeadBeefCafeGameResult? res, string text)
            {
                _result = res ?? default;
                _finalArrayText = text;
            }

            public static bool operator ==(in Result lhs, in Result rhs)
                => lhs._result == rhs._result;
            public static bool operator !=(in Result lhs, in Result rhs) => !(lhs == rhs);
            public override int GetHashCode() => _result.GetHashCode();
            public override bool Equals(object obj) => obj is Result r && r == this;
            public bool Equals(Result r) => r == this;

            private readonly DeadBeefCafeGameResult _result;
            private readonly string _finalArrayText;
        }
    }

    sealed class HpTimeStampProvider : TimeStampProvider
    {
        public static HpTimeStampProvider CreateInstance()
            => new HpTimeStampProvider();

        public override DateTime Now => HpTimesStamps.TimeStampSource.Now;
        public override void Calibrate() =>
            HpTimesStamps.TimeStampSource.Calibrate();

        private HpTimeStampProvider() { }
    }
}
