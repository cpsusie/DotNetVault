using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Xml;
using DotNetVault.Attributes;
using DotNetVault.ClortonGame;
using DotNetVault.DeadBeefCafeBabeGame;
using DotNetVault.Vaults;
using HpTimeStamps;
using JetBrains.Annotations;

namespace CafeBabeGame
{
    //Hi mom
    //[ReportWhiteListLocations]
    class Program
    {
        private static readonly IDeadBeefCafeGameFactory GameFactory = new DeadBeefCafeGameFactorySource().FactoryInstance;
        
        static void Main(string[] args)
        {
            CgTimeStampSource.SupplyAlternateProvider(HpTimeStampProvider.CreateInstance());
            TimeStampSource.Calibrate();
            (bool gotFileOk, string errorInfo, FileInfo outputFile, int numGames, TimeSpan timeout) = GetOutputFile(args);
            Debug.Assert((outputFile != null) == gotFileOk);
            if (gotFileOk)
            {
                Console.WriteLine($"Going to play {numGames} CafeBabe games, each with a time limit of {timeout.TotalSeconds:F5} seconds.  Results will be written to file {outputFile?.FullName ?? "UNSPECIFIED"} ");
                if (numGames > 1)
                {
                    try
                    {
                        PlayMultipleCafeBabeGames(numGames, outputFile, timeout);
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
                        PlayCafeBabeGame(outputFile, timeout);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.ToString());
                        Console.Error.WriteLine("Unexpected error initializing game.  Terminating.");
                        Environment.Exit(-1);
                    }
                }
            }
            else
            {
                Console.Error.WriteLine(
                    "There was a problem parsing the parameters passed hereto." +
                    $"{(!string.IsNullOrEmpty(errorInfo) ? ("  " + errorInfo) : string.Empty)}");
            }

        }

        private static void PlayMultipleCafeBabeGames(int count, FileInfo failureOutputFile, TimeSpan gameTimeLimit)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Parameter not positive.");
            StringBuilder allLog = new StringBuilder();
            for (int i = 1; i <= count; ++i)
            {
                using IBufferBasedOutputHelper outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance();
                {
                    Console.WriteLine($"Starting game {i} of {count}");
                    Result r;
                    using (GameFactory.CreateDeadBeefCafeGame(outputHelper, 3, CafeBabeGame_GameEnded))
                    {
                        r = WaitForGameEndOrTimeout(gameTimeLimit) ?? default;
                        if (r == default)
                        {
                            Console.Error.WriteLine(
                                $"FAILED: The cafe babe game# {i} of {count} faulted.  No results can be retrieved.");
                            string bufferContents = TryGetBufferContents(TimeSpan.FromSeconds(2), outputHelper);
                            LogContents(i, null, bufferContents, failureOutputFile);
                            return;
                        }

                        bool success = r.GameResult?.Success == true;
                        if (success)
                        {
                            Console.WriteLine($"Game {i} of {count} finished ok.");
                            string buffer = outputHelper.GetCurrentTextAndClearBuffer(TimeSpan.FromSeconds(2));
                            if (!ValidateLog(buffer, r.GameResult.Value.FinalArray))
                            {
                                Console.Error.WriteLine("Buffer validation failed.");
                                CafeBabeResultStorage storage = CafeBabeResultStorage.CreateStorageObject(in r, buffer);
                                SerializersDeserializers.SerializeObjectToFile(storage, failureOutputFile);
                                Console.Error.WriteLine("Failing contents written to output file: [" + failureOutputFile + "].");
                                throw new InvalidOperationException("Unable to validate buffer contents.");
                            }

                            allLog.AppendLine($"Game {i} of {count} finished ok.");
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
                                string outputBuffer =
                                    TryGetBufferContents(TimeSpan.FromMilliseconds(500), outputHelper);
                                LogContents(i, in r, outputBuffer ?? string.Empty, failureOutputFile);
                            }

                            Environment.Exit(-1);
                            return;
                        }
                    }


                }

                void LogContents(int gameNum, in Result res, string buff, FileInfo of)
                {
                    Console.WriteLine($"Writing data to file for failed game# {gameNum}.");
                    try
                    {
                        
                        SerializersDeserializers.SerializeObjectToFile(CafeBabeResultStorage.CreateStorageObject(in res, buff), of);
                        Console.WriteLine("Successfully wrote to [" + of.FullName + "].");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("There was an error writing to file [" + of.FullName +
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

        static void PlayCafeBabeGame(FileInfo outputFile, TimeSpan gameMaxTime)
        {
            Result result;
            string bufferContents;
            using IBufferBasedOutputHelper outputHelper = OrderedThreadSafeTestOutputHelper.CreateInstance();
            {
                using IDeadBeefCafeGame game =
                    GameFactory.CreateDeadBeefCafeGame(outputHelper, 3, CafeBabeGame_GameEnded);
                Console.WriteLine("Concrete type of CafeBabe Game: [" + game.GetType().Name + "].");
                
                Result? temp = WaitForGameEndOrTimeout(gameMaxTime);
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
                bool validated = ValidateLog(bufferContents, finalRes.FinalArray);
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

        static bool ValidateLog(string gameLog, ReadOnlyArrayWrapper<UInt256> array)
        {

            if (string.IsNullOrWhiteSpace(gameLog) || array.IsDefault)
            {
                return false;
            }
            var arr = gameLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            const string upTo = "0xDEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE";
            const string oVal =
                "0xC0DE_D00D_FEA2_B00B_C0DE_D00D_FEA2_B00B_C0DE_D00D_FEA2_B00B_C0DE_D00D_FEA2_B00B";
            const string xVal =
                "0xFACE_C0CA_F00D_BAD0_FACE_C0CA_F00D_BAD0_FACE_C0CA_F00D_BAD0_FACE_C0CA_F00D_BAD0";

            int expectedExes = -1;
            int expectedOes = -1;
            var strings = (from str in arr
                           where str.StartsWith("Logged at") && (str.Contains(upTo, StringComparison.OrdinalIgnoreCase) 
                                                                 || str.Contains(xVal, StringComparison.OrdinalIgnoreCase) 
                                                                 || str.Contains(oVal, StringComparison.OrdinalIgnoreCase))
                           select str.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
            bool foundDeadBeef = false, foundCounts = false;
            int indexOfDeadBeef = -1;
            int strIdx = -1;
            foreach (var item in strings)
            {
                ++strIdx;
                switch (item.Length)
                {
                    case 23:
                        if (string.Equals(item[6].Trim(), upTo, StringComparison.OrdinalIgnoreCase))
                        {
                            if (foundDeadBeef)
                            {
                                return false;
                            }
                            indexOfDeadBeef = strIdx;
                            foundDeadBeef = true;
                        }

                        break;
                    case 25:
                        string xCt = item[4];
                        string oCt = item[9];
                        bool parsedOk = int.TryParse(oCt, out expectedOes) && int.TryParse(xCt, out expectedExes);
                        if (!parsedOk)
                        {
                            return false;
                        }
                        foundCounts = true;
                        break;
                }

                if (foundCounts && foundDeadBeef)
                {
                    break;
                }
            }

            if (indexOfDeadBeef < 0 || indexOfDeadBeef >= strings.Length || expectedExes == -1 || expectedOes == -1)
            {
                return false;
            }


            string deadBeef = strings[indexOfDeadBeef].Last();
            bool gotIdx = int.TryParse(deadBeef, out int lastIdxToConsider);
            if (!gotIdx)
            {
                return false;
            }
            var constants = new DeadBeefCafeBabeGameConstants();
            var slice = array.AsSpan().Slice(0, lastIdxToConsider + 1);
            int xCount = 0;
            int oCount = 0;
            int deadBeefCount = 0;
            foreach (ref readonly var value in slice)
            {
                if (value == constants.LookForNumber)
                {
                    ++deadBeefCount;
                }
                else if (value == constants.XNumber)
                {
                    ++xCount;
                }
                else if (value == constants.ONumber)
                {
                    ++oCount;
                }
                else
                {
                    return false;
                }
            }

            int difference = Math.Abs(xCount - oCount);
            if (difference == 0 || difference % 13 != 0)
            {
                return false;
            }

            return deadBeefCount == 1 && xCount == expectedExes &&
                   oCount == expectedOes;
        }

        private static Result?  WaitForGameEndOrTimeout(TimeSpan maxWait)
        {
            maxWait = (maxWait <= TimeSpan.Zero) ? TimeSpan.FromSeconds(1) : maxWait;
            DateTime startedAt = TimeStampSource.Now;
            TimeSpan elapsed = TimeStampSource.Now - startedAt;
            Result res = default;
            
            while (res == default && elapsed <= maxWait)
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
                    elapsed = TimeStampSource.Now - startedAt;
                }
            }

            if (res == default)
            {
                Debug.Assert(elapsed > maxWait);
                Console.Error.WriteLineAsync(
                    "Game did not terminate within time limit.  " +
                    $"Time limit: {maxWait.TotalMilliseconds:F3} milliseconds; Elapsed time: {elapsed.TotalMilliseconds:F3} milliseconds.  " +
                    "Consider increasing the time limit.");
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

        static (bool Success, string ErrorMessage, FileInfo OutputFile, int NumGamges, TimeSpan MaxGameLength)
            GetOutputFile(string[] args)
        {
            TimeSpan maxGameLength = TimeSpan.FromSeconds(7.5);
            FileInfo outputFile = null;
            string errorInfo = string.Empty;
            int numGames = 1;
            bool gotNumGames = (args?.Length > 1) && (int.TryParse(args[1], out numGames) || (args[1].StartsWith("/") &&
                int.TryParse(args[1].AsSpan().Slice(1, args[1].Length - 1), out numGames)));
            numGames = gotNumGames && numGames > 1 ? numGames : 1;
            string fileName;
            string defaultFn = numGames > 1 ? TheDefaultFailureFile : TheDefaultFileName;
            fileName = (args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) ? args[0] : defaultFn;
            if (fileName.StartsWith("/"))
            {
                fileName = fileName.Substring(1, fileName.Length - 1);
            }

            maxGameLength = GetMaxGameLength(args, maxGameLength);
            

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

            return (outputFile != null, errorInfo, outputFile, numGames, maxGameLength);

            static TimeSpan GetMaxGameLength(string[] args, TimeSpan fallback)
            {
                TimeSpan ret = fallback;
                if (args?.Length > 2)
                {
                    ReadOnlySpan<char> timeStr = true == args[2]?.StartsWith("/") ? args[2].AsSpan().Slice(1, args[2].Length - 1) : args[2]?.Trim() ?? string.Empty;
                    double millisecondsParsed;
                    bool parsedThem = double.TryParse(timeStr, out millisecondsParsed) && !double.IsInfinity(millisecondsParsed) && !double.IsNaN(millisecondsParsed) && millisecondsParsed > 0;
                    if (parsedThem)
                    {
                        ret = TimeSpan.FromMilliseconds(millisecondsParsed);
                    }
                }
                return ret;
            }
        }
        
        #region For debugging failed results
        //static CafeBabeResultStorage Deserialize([NotNull] FileInfo source)
        //{
        //    try
        //    {
        //        return SerializersDeserializers.DeserializeObjectFromFile<CafeBabeResultStorage>(source);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.Error.WriteLine("Unable to deserialize from file: [" + source.FullName + "].  Exception: [" + ex + "].");
        //        throw;
        //    }
        //}

        //static void RevalidateResults([NotNull] CafeBabeResultStorage storage)
        //{
        //    Console.WriteLine("Revalidating results.");
        //    string gameResult = GetTextResult();
        //    Console.WriteLine("Game result: [" + gameResult + "].");
        //    if (storage.GameResult.GameResult != null)
        //        Console.WriteLine("Detail: [" + storage.GameResult.GameResult + "].");
        //    Console.WriteLine("Validating....");

        //    bool success = ValidateLog(storage.Buffer,
        //        storage.GameResult.GameResult?.FinalArray ?? ReadOnlyArrayWrapper<UInt256>.Default);
        //    if (!success)
        //    {
        //        Console.Error.WriteLine("Validation failed.");
        //    }
        //    else
        //    {
        //        Console.WriteLine("Validation succeeded.");
        //    }
        //    string GetTextResult()
        //    {
        //        if (storage.GameResult.GameResult == null)
        //        {
        //            return "NO GAME RESULT";
        //        }
        //        if (storage.GameResult.GameResult.Value.Success)
        //        {
        //            return "SUCCESS";
        //        }
        //        if (storage.GameResult.GameResult.Value.Cancelled)
        //        {
        //            return "CANCELLED";
        //        }
        //        return "FAILURE";
        //    }

        //}

        //static void DoSerializationTest()
        //{
        //    try
        //    {
        //        FileInfo target = new FileInfo("serialization_test_target.xml");
        //        ReadOnlyArrayWrapper<UInt256> items = ReadOnlyArrayWrapper<UInt256>.CreateReadonlyArray(new[]
        //        {
        //            new UInt256(0xCAFE_BABE_CAFE_BABE, 0xCAFE_BABE_CAFE_BABE, 0xCAFE_BABE_CAFE_BABE,
        //                0xCAFE_BABE_CAFE_BABE),
        //            new UInt256(0xC0DE_D00D_FEA2_B00B, 0xC0DE_D00D_FEA2_B00B, 0xC0DE_D00D_FEA2_B00B,
        //                0xC0DE_D00D_FEA2_B00B),
        //        });
        //        SerializersDeserializers.SerializeObjectToFile(items, target);
        //        target.Refresh();
        //        if (!target.Exists)
        //            throw new FileNotFoundException("The specified file was not found indicating it was not created.",
        //                target.FullName);

        //        ReadOnlyArrayWrapper<UInt256> roundTripped =
        //            SerializersDeserializers.DeserializeObjectFromFile<ReadOnlyArrayWrapper<UInt256>>(target);
        //        bool success = roundTripped.SequenceEqual(items);
        //        string results = success ?
        //             "The serialization test succeeded: object successfully round-tripped."
        //            : "The test failed because the deserialized contents are not identical to the serialized contents.";
        //        if (success)
        //            Console.WriteLine(results);
        //        else
        //            Console.Error.WriteLine(results);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.Error.WriteLine("Serialization test failed.  Exception contents: [" + ex + "].");
        //        throw;
        //    }

        //} 
        #endregion

        private const string TheDefaultFileName = "Cafe_Babe_Game_Results.txt";
        private const string TheDefaultFailureFile = "Cafe_Babe_Game_Failure.xml";
        private static readonly BasicMonitorVault<Result> TheResults = new BasicMonitorVault<Result>(default, TimeSpan.FromMilliseconds(100));
    }

    [DataContract]
    public sealed class CafeBabeResultStorage : IEquatable<CafeBabeResultStorage>
    {
        public static CafeBabeResultStorage CreateStorageObject(in Result r, [CanBeNull] string bufferContents) => new CafeBabeResultStorage(in r, bufferContents);

        public ref readonly Result GameResult => ref _result;
        [NotNull] public string Buffer => _buffer ?? string.Empty;
        public DateTime Timestamp => _timestamp;

        private CafeBabeResultStorage(in Result r, string buffer)
        {
            _result = r;
            _buffer = buffer ?? string.Empty;
            _timestamp = r.GameResult?.EndedAt ?? TimeStampSource.Now;
        }

        public override string ToString() => "CafeBabeResultStorage of Game at [" + _timestamp.ToString("O") + "].";

        public bool Equals(CafeBabeResultStorage other) =>
            other != null && other._result == _result && other._timestamp == _timestamp;

        public override int GetHashCode()
        {
            int hash = _timestamp.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ _result.GetHashCode();
            }
            return hash;
        }

        public override bool Equals(object obj) => Equals(obj as CafeBabeResultStorage);

        public static bool operator ==(CafeBabeResultStorage lhs, CafeBabeResultStorage rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (ReferenceEquals(lhs, null)) return false;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(CafeBabeResultStorage lhs, CafeBabeResultStorage rhs) => !(lhs == rhs);

        [DataMember] private readonly DateTime _timestamp;
        [DataMember] private readonly string _buffer;
        [DataMember] private readonly Result _result;
    }

    [DataContract]
    [VaultSafe]
    public readonly struct Result : IEquatable<Result>
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

        [DataMember] private readonly DeadBeefCafeGameResult _result;
        [DataMember] private readonly string _finalArrayText;
    }

    sealed class HpTimeStampProvider : TimeStampProvider
    {
        public static HpTimeStampProvider CreateInstance()
            => new HpTimeStampProvider();

        public override DateTime Now => TimeStampSource.Now;
        public override void Calibrate() =>
            TimeStampSource.Calibrate();

        private HpTimeStampProvider() { }
    }

    static class SerializersDeserializers
    {
        public static void SerializeObjectToFile<T>(T serializeMe, [NotNull] FileInfo target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            DataContractSerializer serializer  = new DataContractSerializer(typeof(T));
            {
                using var sw = target.Create();
                serializer.WriteObject(sw, serializeMe);
            }
            target.Refresh();
        }

        public static T DeserializeObjectFromFile<T>([NotNull] FileInfo source)
        {
            DataContractSerializer deserializer = new DataContractSerializer(typeof(T));
            using var sr = source.Open(FileMode.Open);
            XmlDictionaryReader reader =
                XmlDictionaryReader.CreateTextReader(sr,
                    new XmlDictionaryReaderQuotas() {MaxStringContentLength = (1_024 * 1_024 * 100)});
            return (T) deserializer.ReadObject(reader, true);
        }
    }
}
