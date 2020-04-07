using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DotNetVault.ClortonGame;
using DotNetVault.Vaults;
using HpTimesStamps;

namespace ClortonGameDemo
{
    class Program
    {
        private static IClortonGameFactory GameFactory { get; set; }

        static void Main(string[] args)
        {
            CgTimeStampSource.SupplyAlternateProvider(HpTimeStampProvider.CreateInstance());
            TimeStampSource.Calibrate();
            var gameFactories = new ClortonGameFactorySource();
            (bool gotFileOk, string errorGettingFile, FileInfo outputFile, int numGames, VaultType varietyOfVault) = GetOutputFile(args);
            GameFactory = varietyOfVault == VaultType.Basic
                ? gameFactories.BasicVaultGameFactory
                : gameFactories.CustomVaultGameFactory;
            if (numGames == 1)
            {
                Console.WriteLine("Type of vault selected: [" + varietyOfVault + "].");
            }

            Debug.Assert(gotFileOk == (outputFile != null));
            if (gotFileOk)
            {
                if (numGames > 1)
                {
                    try
                    {
                        PlayMultipleClortonGames(numGames, outputFile);
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
                        PlayClortonGame(outputFile);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.ToString());
                        Console.Error.WriteLine("Unexpected error initializing game.  Terminating.");
                        Environment.Exit(-1);
                    }
                }
            }
            else
            {
                Console.Error.WriteLine("There was a problem with the output file.  Additional info: [" +
                                        errorGettingFile + "].");
            }
        }

        private static void PlayMultipleClortonGames(int count, FileInfo outputFile)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Parameter not positive.");

            string failingBufferContents = string.Empty;
            VaultType oddVaultType = TheRng.Next(0, 2) == 0 ? VaultType.Basic : VaultType.Custom;
            VaultType evenVaultType = oddVaultType == VaultType.Basic ? VaultType.Custom : VaultType.Basic;
            for (int i = 1; i <= count; ++i)
            {
                using IBufferBasedOutputHelper outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance();
                {
                    Console.WriteLine($"Starting game {i} of {count}");
                    ClortonGameResult? result;
                    var factory = GetGameFactory(i, evenVaultType);
                    using (IClortonGame game = factory.CreateClortonGame(outputHelper, 3, ClortonGame_GameEnded))
                    {
                        Console.WriteLine("Concrete type of clorton game: [" + game.GetType().Name + "].");
                        result = WaitForGameEndOrTimeout(TimeSpan.FromSeconds(3));

                        if (result == null)
                        {
                            Console.Error.WriteLine(
                                $"FAILED: The clorton game# {i} of {count} faulted.  No results can be retrieved.");
                            string bufferContents = TryGetBufferContents(TimeSpan.FromSeconds(2), outputHelper);
                            LogContents(i, null, bufferContents, outputFile);
                            return;
                        }

                        ClortonGameResult finalRes = result.Value;
                        bool success = finalRes.Success && ValidateFinalResult(in finalRes);
                        if (success)
                        {
                            Console.WriteLine($"Game {i} of {count} finished ok.");
                        }
                        else
                        {
                            Console.WriteLine($"FAULT Game {i} of {count} FAULTED.");
                            if (finalRes.Cancelled)
                            {
                                Console.WriteLine("The game was cancelled.");
                            }
                            else
                            {
                                Console.WriteLine("There were {0} xes and {1} oes.", finalRes.XCount,
                                    finalRes.OCount);
                                Console.WriteLine(finalRes.WinningThreadIndex != null
                                    ? ("The winning reader thread was thread at index " +
                                       finalRes.WinningThreadIndex.Value + ".")
                                    : "There was no winning thread.");
                            }
                            Environment.Exit(-1);
                            return;
                        }
                    }
                }
            }

            void LogContents(int gameNum, in ClortonGameResult? res, string buff, FileInfo of)
            {
                Console.WriteLine($"Writing final string and logs to file for failed game# {gameNum}.");
                try
                {
                    using (var sw = of.CreateText())
                    {
                        sw.WriteLine("The final string was:");
                        sw.WriteLine(res?.FinalString ?? string.Empty);
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
                    ret= string.Empty;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    string moreToSay = "Unable to retrieve logs from the output helper " +
                                       "because of unexpected exception.  No results file can be written.";
                    Console.Error.WriteLine(moreToSay);
                    ret= string.Empty;
                }

                return ret;
            }

            static IClortonGameFactory GetGameFactory(int idx, VaultType evenVaultType)
            {
                IClortonGameFactory ret;
                var factory = new ClortonGameFactorySource();
                bool isEven = idx % 2 == 0;
                if (isEven)
                {
                    ret = evenVaultType == VaultType.Basic
                        ? factory.BasicVaultGameFactory
                        : factory.CustomVaultGameFactory;
                }
                else
                {
                    ret = evenVaultType == VaultType.Basic
                        ? factory.CustomVaultGameFactory
                        : factory.BasicVaultGameFactory;
                }
                return ret;
            }
        }

        private static void PlayClortonGame(FileInfo outputFile)
        {
            ClortonGameResult? result;
            string bufferContents;
            using IBufferBasedOutputHelper outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance();
            {
                using IClortonGame game = GameFactory.CreateClortonGame(outputHelper, 3, ClortonGame_GameEnded);
                Console.WriteLine("Concrete type of Clorton Game: [" + game.GetType().Name + "].");
                result = WaitForGameEndOrTimeout(TimeSpan.FromSeconds(3));
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

            if (result == null)
            {
                Console.Error.WriteLine("The clorton game faulted.  No results can be retrieved.");
                Environment.Exit(-1);
                return;
            }

            ClortonGameResult finalRes = result.Value;
            Console.WriteLine(finalRes.Success ? "The game was successful!" : "The game FAILED.");
            if (finalRes.Success)
            {
                ValidateFinalResult(in finalRes);
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
                    sw.WriteLine("The final string was:");
                    sw.WriteLine(finalRes.FinalString ?? string.Empty);
                    sw.WriteLine("END FINAL STRING");

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

        private static bool ValidateFinalResult(in ClortonGameResult finalRes)
        {
            Console.WriteLine();
            Console.WriteLine("Validating correctness of final result.");
            int firstIdx = finalRes.FinalString.IndexOf(ClortonGame.GameConstants.LookForText, StringComparison.Ordinal);
            if (firstIdx < 0)
            {
                Console.Error.WriteLine("UNABLE TO VALIDATE RESULTS.  POSSIBLE FLAW IN VAULT.");
                Console.Error.WriteLine(ClortonGame.GameConstants.LookForText + " not found!");
                return false;
            }
            else
            {
                var preClortonSpan = finalRes.FinalString.AsSpan(0, firstIdx);
                int xCount = 0;
                int oCount = 0;
                foreach (var c in preClortonSpan)
                {
                    if (c == ClortonGame.GameConstants.XChar)
                        ++xCount;
                    else if (c == ClortonGame.GameConstants.OChar)
                        ++oCount;
                }

                Console.WriteLine($"Before {ClortonGame.GameConstants.LookForText}, there were {xCount} xes and {oCount} oes.");
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
                else
                {
                    Console.Error.WriteLine(
                        "The difference between the two is NOT evenly divisible by thirteen.  WARNING RESULTS NOT VALIDATED.  POSSIBLE FLAW IN VAULT LOGIC.");
                    Console.Error.WriteLine();
                    return false;
                }
            }
            
        }

        private static ClortonGameResult? WaitForGameEndOrTimeout(TimeSpan maxWait)
        {
            maxWait = (maxWait <= TimeSpan.Zero) ? TimeSpan.FromSeconds(1) : maxWait;
            DateTime quitAfter = TimeStampSource.Now + maxWait;
            ClortonGameResult? res = null;
            while (res == null && TimeStampSource.Now <= quitAfter)
            {
                try
                {
                    using var lck = TheResults.Lock();
                    res = lck.Value;
                    lck.Value = null;
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

        private static void ClortonGame_GameEnded(object sender, ClortonGameEndedEventArgs e)
        {
            if (e != null)
            {
                using var lck = TheResults.Lock();
                if (lck.Value != default) throw new Exception("Duplicate results");
                if (e.Results != default)
                {
                    lck.Value = e.Results;
                }
            }
        }

        static (bool Success, string ErrorMessage, FileInfo OutputFile, int NumGamges, VaultType VaultVariety) 
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

            return (outputFile != null, errorInfo, outputFile, numGames,
                TheRng.Next(0, 2) == 0 ? VaultType.Basic : VaultType.Custom);

        }

        private enum VaultType
        {
            Basic=0,
            Custom
        }

        private static readonly Random TheRng = new Random();
        private const VaultType DefaultVaultType = VaultType.Custom;
        private const string TheDefaultFileName = "Clorton_Game_Results.txt";
        private static readonly BasicMonitorVault<ClortonGameResult?> TheResults = new BasicMonitorVault<ClortonGameResult?>(default, TimeSpan.FromMilliseconds(100));
    }
    
    
    
}
