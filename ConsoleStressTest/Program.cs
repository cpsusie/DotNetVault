using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;

namespace ConsoleStressTest
{
    class Program
    {
        static Program() => TheResultText = new LocklessWriteOnce<string>();

        static void Main(string [] args)
        {
            var getArgumentsRes = StressTestArgs.CreateArguments(args);
            if (getArgumentsRes.Arguments.HasValue)
            {
                var arguments = getArgumentsRes.Arguments.Value;
                Console.CancelKeyPress += Console_CancelKeyPress;
                CancellationToken token = TheCts.Token;
                try
                {
                    Console.WriteLine($"Executing simulation with the following arguments: [{arguments.ToString()}]:");
                    LogPrecisionOfTimestampSource();
                    Console.WriteLine($"Using sync mechanism: [{Configuration<StressTestObject>.FactoryType}]");
                    TimeSpan updateEvery = TimeSpan.FromSeconds(2);
                    int numThreads = arguments.NumberThreads;
                    int numActionsPerThread = arguments.ActionsPerThread;
                    using (IStressSimulation simulation =
                        StressSimulation.CreateStressSimulation(numThreads, numActionsPerThread))
                    {
                        simulation.Done += Simulation_Done;
                        Console.WriteLine("Starting simulation...");
                        DateTime startAt = TimeStampSource.Now;
                        DateTime nextUpdate = startAt + updateEvery;
                        simulation.StartSimulation();
                        while (!TheResultText.IsSet)
                        {
                            token.ThrowIfCancellationRequested();
                            DateTime now = TimeStampSource.Now;
                            if (TimeStampSource.Now >= nextUpdate)
                            {
                                Console.WriteLine($"Simulation pending for {(now - startAt).TotalSeconds:F3} seconds.");
                                nextUpdate += updateEvery;
                            }

                            Thread.Sleep(TimeSpan.FromMilliseconds(10));
                        }

                        if (arguments.TargetFile != null)
                        {
                            try
                            {
                                Console.WriteLine("Writing results to file specified...");
                                using (var sw = arguments.TargetFile.CreateText())
                                {
                                    sw.WriteLine(PrecisionStatusText);
                                    sw.WriteLine(TheResultText.Value);
                                }
                                arguments.TargetFile.Refresh();
                                if (arguments.TargetFile.Exists)
                                {
                                    Console.WriteLine($"Wrote results to: [{arguments.TargetFile.FullName}].");
                                }
                                else
                                {
                                    ErrorPostResultsToConsole(TheResultText.Value, null, arguments.TargetFile);
                                    Environment.Exit(-1);
                                }
                            }
                            catch (Exception ex)
                            {
                                ErrorPostResultsToConsole(TheResultText.Value, ex, arguments.TargetFile);
                                Environment.Exit(-1);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Logging results: ");
                            Console.WriteLine(TheResultText.Value);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("The simulation was terminated.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"The simulation faulted.  Error info: [{ex}].");
                    Environment.Exit(-1);
                }
            }
            else
            {
                Console.Error.WriteLine("Invalid arguments supplied.");
                Console.Error.WriteLine(getArgumentsRes.ErrorText ?? string.Empty);
                Environment.Exit(-1);
            }


            static void ErrorPostResultsToConsole(string logMe, Exception error, FileInfo fi)
            {
                Debug.Assert(logMe != null && fi != null);
                Console.Error.Write($"Unable to write results to target file [{fi.FullName}].");
                if (error != null)
                {
                    Console.Error.Write($"  Error was because of exception: [{error}].");
                    Console.Error.WriteLine();
                }
                Console.Error.WriteLine($"Will log to console instead: ");
                Console.Error.Write(logMe);
                Console.Error.WriteLine();
            }
        }
        private static void LogPrecisionOfTimestampSource()
            =>  Console.WriteLine(PrecisionStatusText);
        

        private static string PrecisionStatusText =>
            "Precision of timestamps in simulation: " +
            $"[{(TimeStampSource.IsHighPrecision ? "HIGH PRECISION" : "LOW PRECISION")}].";
        

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            TheCts.Cancel();
        }

        private static void Simulation_Done(object sender, StressSimDoneEventArgs e)
        {
            TheResultText.SetOrThrow(e?.ToString() ?? "NULL RESULT");
            Console.WriteLine($"RECEIVED DONE SIGNAL AT: [{TimeStampSource.Now:O}].");
        }

        private static readonly LocklessWriteOnce<string> TheResultText;
        private static readonly CancellationTokenSource TheCts = new CancellationTokenSource();
    }

    public readonly struct StressTestArgs : IEquatable<StressTestArgs>, IComparable<StressTestArgs>
    {
        public const int DefaultNumThreads = 6;
        public const int DefaultActionsPerThread = 100;
        
        public static (StressTestArgs? Arguments, string ErrorText) CreateArguments([CanBeNull] string[] args)
        {
            DateTime ts = TimeStampSource.Now;
            StressTestArgs? ret;
            string errorText;
            ImmutableArray<string> separatedArgs = args?.Where(str => !string.IsNullOrWhiteSpace(str)).ToImmutableArray() ??
                                                   ImmutableArray<string>.Empty;
            if (separatedArgs.Any())
            {
                StringBuilder errorLog = new StringBuilder();
                var parseRes= TokenizeArgs(separatedArgs).ToArray();
                var errorResults = parseRes.Where(res => res.Token == Token.Error).ToArray();
                if (errorResults.Any())
                {
                    errorLog.AppendLine("Did not understand arguments: ");
                    foreach (var res in errorResults)
                    {
                        errorLog.AppendLine(
                            $"Error token parsing token: [{res.Value ?? "NULL"}], Error description: [{res.ErrorText ?? "NO INFO AVAILABLE"}]");
                    }
                    AppendUsageInfo(errorLog);
                    ret = null;
                    errorText = errorLog.ToString();
                }
                else
                {
                    SortedSet<Token> tokens = new SortedSet<Token>(parseRes.Select(tkn => tkn.Token));
                    if (tokens.Count != parseRes.Length)
                    {
                        errorLog.Append("One or more duplicate tokens identified in parameters.");
                        AppendUsageInfo(errorLog);
                        errorText = errorLog.ToString();
                        ret = null;
                    }
                    else
                    {
                        bool error = false;
                        FileInfo fi=null;
                        int? numThreads=null;
                        int? actions=null;
                        errorLog.Clear();
                        foreach (var value in parseRes)
                        {
                            switch (value.Token)
                            {
                                default:
                                    errorLog.AppendLine($"Unrecognized token: {value.Token.ToString()}");
                                    error = true;
                                    break;
                                case Token.FileName:
                                    try
                                    {
                                        fi = new FileInfo(value.Value);
                                    }
                                    catch (Exception ex)
                                    {
                                        errorLog.AppendLine(
                                            $"Unable to parse file argument [{value.Value ?? "NULL"}] to a valid file. Exception: [{ex}].");
                                        error = true;
                                    }
                                    break;
                                case Token.NumThreads:
                                    bool parsedIt = int.TryParse(value.Value, out int threads) && threads > 0;
                                    if (parsedIt)
                                    {
                                        numThreads = threads;
                                    }
                                    else
                                    {
                                        error = true;
                                        errorLog.AppendLine(
                                            $"Argument to number of threads parameter (value: {value.Value ?? "NULL"}) not parseable as [{typeof(int).FullName}].");
                                    }
                                    break;
                                case Token.ActionsPerThread:
                                    bool parsedActions = int.TryParse(value.Value, out int acts) && acts> 0;
                                    if (parsedActions)
                                    {
                                        actions= acts;
                                    }
                                    else
                                    {
                                        error = true;
                                        errorLog.AppendLine(
                                            $"Argument to actions/thread parameter (value: {value.Value ?? "NULL"}) not parseable as [{typeof(int).FullName}].");
                                    }
                                    break;

                            }

                        }

                        if (error)
                        {
                            AppendUsageInfo(errorLog);
                            ret = null;
                            errorText = errorLog.ToString();
                        }
                        else
                        {
                            ret = new StressTestArgs(ts, numThreads ?? DefaultNumThreads, actions ?? DefaultActionsPerThread, fi);
                            errorText = string.Empty;
                        }
                    }
                    
                }
            }
            else
            {
                ret = new StressTestArgs(ts, DefaultNumThreads, DefaultActionsPerThread, null);
                errorText = string.Empty;
            }

            return (ret, errorText);

            static void AppendUsageInfo(StringBuilder appendToMe)
            {
                appendToMe.AppendLine("Recognized flags: ");
                appendToMe.AppendLine($"\tToken: [{FileToken}].\t Usage: [{FileToken} outputfile.txt]");
                appendToMe.AppendLine($"\tToken: [{ThreadNumToken}].\t Usage: [{ThreadNumToken} 5] (must be positive integer value).");
                appendToMe.AppendLine($"\tToken: [{ActionsPerThreadToken}].\t Usage: [{ActionsPerThreadToken} 12] (must be positive integer value).");
            }

            

        }

        public readonly int NumberThreads;
        public readonly int ActionsPerThread;
        [CanBeNull] public readonly FileInfo TargetFile;
        public readonly DateTime RequestedTimeStamp;

        static StressTestArgs() => TheTokenLookup = new LocklessLazyWriteOnce<ImmutableDictionary<string, Token>>(InitDictionary);

        private StressTestArgs(DateTime timestamp, int numThreads, int actionsPerThread, [CanBeNull] FileInfo targetFile)
        {
            RequestedTimeStamp = timestamp;
            NumberThreads = numThreads > 0
                ? numThreads
                : throw new ArgumentOutOfRangeException(nameof(numThreads), numThreads, "Parameter must be positive.");
            ActionsPerThread = actionsPerThread > 0
                ? actionsPerThread
                : throw new ArgumentOutOfRangeException(nameof(actionsPerThread), actionsPerThread,
                    "Parameter must be positive.");
            if (targetFile != null)
            {
                targetFile.Refresh();
                if (targetFile.Exists && targetFile.IsReadOnly)
                {
                    throw new ArgumentException($"The supplied file (name: [{targetFile.FullName}]) is read-only.");
                }
            }
            TargetFile = targetFile;
        }

        public static bool operator ==(in StressTestArgs lhs, in StressTestArgs rhs) =>
            lhs.RequestedTimeStamp == rhs.RequestedTimeStamp && lhs.NumberThreads == rhs.NumberThreads &&
            lhs.ActionsPerThread == rhs.ActionsPerThread && lhs.TargetFile?.FullName == rhs.TargetFile?.FullName;
        public static bool operator !=(in StressTestArgs lhs, in StressTestArgs rhs) => !(lhs == rhs);
        public override int GetHashCode() => RequestedTimeStamp.GetHashCode();
        public bool Equals(StressTestArgs other) => this == other;
        public override bool Equals(object other) => (other as StressTestArgs?) == this;
        public int CompareTo(StressTestArgs other) => Compare(in this, in other);
        public static bool operator >(in StressTestArgs lhs, in StressTestArgs rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in StressTestArgs lhs, in StressTestArgs rhs) => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in StressTestArgs lhs, in StressTestArgs rhs) => !(lhs < rhs);
        public static bool operator <=(in StressTestArgs lhs, in StressTestArgs rhs) => !(lhs > rhs);
        public override string ToString() =>
            $"Stress Test Args -- Request time: [{RequestedTimeStamp:O}], TargetFile: " +
            $"[{TargetFile?.FullName ?? "CONSOLE"}], # Threads: [{NumberThreads.ToString()}], " +
            $"Actions/Thread: [{ActionsPerThread.ToString()}].";

        private static int Compare(in StressTestArgs lhs, in StressTestArgs rhs)
        {
            int ret;
            int stampComparison = lhs.RequestedTimeStamp.CompareTo(rhs.RequestedTimeStamp);
            if (stampComparison == 0)
            {
                int fileComparison = CompareFileInfos(lhs.TargetFile, rhs.TargetFile);
                if (fileComparison == 0)
                {
                    int threadComparison = lhs.NumberThreads.CompareTo(rhs.NumberThreads);
                    ret = threadComparison == 0
                        ? lhs.ActionsPerThread.CompareTo(rhs.ActionsPerThread)
                        : threadComparison;
                }
                else
                {
                    ret = fileComparison;
                }
            }
            else
            {
                ret = stampComparison;
            }

            return ret;

            static int CompareFileInfos(FileInfo l, FileInfo r)
            {
                if (ReferenceEquals(l, r)) return 0;
                if (ReferenceEquals(l, null)) return -1;
                if (ReferenceEquals(r, null)) return 1;
                return string.Compare(l.FullName, r.FullName, StringComparison.Ordinal);
            }
        }

        private static readonly LocklessLazyWriteOnce<ImmutableDictionary<string, Token>> TheTokenLookup;

        private static ImmutableDictionary<string, Token> InitDictionary()
        {
            var dictionary =
                ImmutableDictionary.CreateBuilder<string, Token>(TrimmingStringComparer.TrimmingOrdinalIgnoreCase);
            dictionary.Add(ActionsPerThreadToken, Token.ActionsPerThread);
            dictionary.Add(ThreadNumToken, Token.NumThreads);
            dictionary.Add(FileToken, Token.FileName);
            return dictionary.ToImmutable();
        }

        private static IEnumerable<(Token Token, string Value, string ErrorText)> TokenizeArgs(ImmutableArray<string> args)
        {
            for (int i = 0; i < args.Length; )
            {
                bool isToken = TheTokenLookup.Value.TryGetValue(args[i], out Token token);
                if (isToken)
                {
                    switch (token)
                    {
                        default:
                        case Token.Error:
                            yield return (token, args[i], $"Unable to understand token: [{args[i]}].");
                            ++i;
                            break;
                        case Token.FileName:
                            if (args.Length == i + 1)
                            {
                                yield return (Token.Error, string.Empty,
                                    $@"No file name argument supplied for token [{Token.Error.ToString()}].");
                                ++i;
                            }
                            else
                            {
                                yield return (Token.FileName, args[i + 1], string.Empty);
                                i += 2;
                            }
                            break;
                        case Token.NumThreads:
                            if (args.Length == i + 1)
                            {
                                yield return (Token.Error, string.Empty,
                                    $@"No number of threads argument supplied for token [{Token.NumThreads.ToString()}].");
                                ++i;
                            }
                            else
                            {
                                yield return (Token.NumThreads, args[i + 1], string.Empty);
                                i += 2;
                            }
                            break;
                        case Token.ActionsPerThread:
                            if (args.Length == i + 1)
                            {
                                yield return (Token.Error, string.Empty,
                                    $@"No actions per thread argument supplied for token [{Token.ActionsPerThread.ToString()}].");
                                ++i;
                            }
                            else
                            {
                                yield return (Token.ActionsPerThread, args[i + 1], string.Empty);
                                i += 2;
                            }
                            break;
                    }
                }
                else
                {
                    yield return (Token.Error, args[i], $"Did not understand argument: [{args[i]}].");
                    i += 1;
                }
            }
        }
        

        private const string ActionsPerThreadToken = "/ActionsThread";
        private const string ThreadNumToken = "/NumThreads";
        private const string FileToken = "/FileName";
    }

    public sealed class TrimmingStringComparer : StringComparer
    {
        public static StringComparer TrimmingOrdinal { get; } = new TrimmingStringComparer(StringComparer.Ordinal);
        public static StringComparer TrimmingOrdinalIgnoreCase { get; } = new TrimmingStringComparer(StringComparer.OrdinalIgnoreCase);

        public override int Compare(string x, string y) =>
            _baseComparer.Compare(x?.Trim() ?? string.Empty, y?.Trim() ?? string.Empty);
        public override bool Equals(string x, string y) =>
            _baseComparer.Equals(x?.Trim() ?? string.Empty, y?.Trim() ?? string.Empty);
        public override int GetHashCode([CanBeNull] string obj) =>
            _baseComparer.GetHashCode(obj?.Trim() ?? string.Empty);

        private TrimmingStringComparer([NotNull] StringComparer baseComparer) =>
            _baseComparer = baseComparer ?? throw new ArgumentNullException(nameof(baseComparer));

        [NotNull] private readonly StringComparer _baseComparer;

        
    }

    public enum Token
    {
        Error = 0,
        FileName,
        NumThreads,
        ActionsPerThread
    }
}
