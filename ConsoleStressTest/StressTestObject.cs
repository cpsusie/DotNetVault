using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace ConsoleStressTest
{
    sealed class StressTestObject
    {
        [NotNull]
        public Result GetResult()
        {
            string text = _listOfStringInsertions.ToString();
            ImmutableSortedSet<StressTestEntry> entries = _entryRegistration.ToImmutable();
            return new Result(entries, text);
        }

        public void Register(int actionNumber)
        {
            StressTestEntry entry = StressTestEntry.CreateStressTestEntry(actionNumber);
            bool registered = _entryRegistration.Add(entry);
            if (!registered)
            {
                throw new InvalidOperationException($"An entry already exists matching: [{entry.ToString()}]");
            }

            _listOfStringInsertions.Append(entry.Text);
        }

        public StressTestObject()
        {
            _entryRegistration = ImmutableSortedSet.CreateBuilder<StressTestEntry>();
            _listOfStringInsertions = new StringBuilder();
        }

        private readonly StringBuilder _listOfStringInsertions;
        private readonly ImmutableSortedSet<StressTestEntry>.Builder _entryRegistration;
    }

    [VaultSafe]
    sealed class Result
    {
        [NotNull] public ImmutableSortedSet<StressTestEntry> Entries { get; }
        [NotNull] public string TextResult { get; }

        internal Result([NotNull] ImmutableSortedSet<StressTestEntry> results, [NotNull] string textResult)
        {
            Entries = results ?? throw new ArgumentNullException(nameof(results));
            TextResult = textResult ?? throw new ArgumentNullException(nameof(textResult));
        }

        public (bool Success, string Report) CreateResults(int numThreads, int actionsPerThread)
        {
            string report;
            bool success = true;
            StringBuilder sbLog = new StringBuilder();
            StringBuilder sbEntries = new StringBuilder();
            SortedSet<int> threadIds = new SortedSet<int>(Entries.Select(entry => entry.ManagedThreadId).Distinct());
            var managedThreadIdAndExpectedEntries = new SortedDictionary<int, int>((from id in threadIds
                let key = id
                let value = actionsPerThread
                select new KeyValuePair<int, int>(key, value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            if (managedThreadIdAndExpectedEntries.Count != numThreads)
            {
                success = false;
                sbLog.AppendLine($"FAILURE.  There were missing thread ids from the entries.  Expected {numThreads} thread ids but only found the following thread ids:");
                foreach (var item in managedThreadIdAndExpectedEntries.Keys)
                {
                    sbLog.AppendLine($"Thread id: {item}");
                }

                sbLog.AppendLine("Listing all entries: ");
                Entries.ApplyToAll(entr => sbLog.AppendLine(entr.ToString()));
                sbLog.AppendLine();
                sbLog.AppendLine("Logging text output: ");
                sbLog.AppendLine(TextResult);
                sbLog.AppendLine("Done logging FAILED simulation.");
                report = sbLog.ToString();

            }
            else
            {
                try
                {
                    foreach (StressTestEntry entry in Entries)
                    {
                        if (managedThreadIdAndExpectedEntries.ContainsKey(entry.ManagedThreadId))
                        {
                            if (FindMatchingText(in entry, sbLog))
                            {
                                managedThreadIdAndExpectedEntries[entry.ManagedThreadId] =
                                    managedThreadIdAndExpectedEntries[entry.ManagedThreadId] - 1;
                                sbEntries.AppendLine(entry.ToString());
                            }
                            else
                            {
                                success = false;
                            }
                        }
                        else
                        {
                            sbLog.AppendLine(
                                $"Unable to find managed thread id [{entry.ManagedThreadId}] while reading entry: [{entry.ToString()}].");
                            success = false;
                        }
                    }

                    if (success)
                    {
                        var threadIdsMissingEntries = managedThreadIdAndExpectedEntries.Where(kvp => kvp.Value > 0)
                            .Select(kvp => kvp.Key).ToImmutableSortedSet();
                        if (threadIdsMissingEntries.Any())
                        {
                            success = false;
                            foreach (var item in threadIdsMissingEntries)
                            {
                                sbLog.AppendLine(
                                    $"There remain {managedThreadIdAndExpectedEntries[item]} entries unaccounted for for managed thread id: [{item}].");
                            }

                            sbLog.AppendLine("Logging entries: ");
                            sbLog.Append(sbEntries);
                            sbLog.AppendLine();
                            sbLog.AppendLine("Logging text output: ");
                            sbLog.AppendLine(TextResult);
                            sbLog.AppendLine();
                            sbLog.AppendLine();
                            sbLog.AppendLine("Done logging FAILED simulation.");
                            report = sbLog.ToString();
                        }
                        else
                        {
                            if (numThreads * actionsPerThread != Entries.Count)
                            {
                                throw new InvalidOperationException(
                                    $"Expected number of entries to be ({nameof(numThreads)} * {nameof(actionsPerThread)}) [{numThreads} * {actionsPerThread} == {numThreads * actionsPerThread}]; actual: [{Entries.Count}].");
                            }
                            sbLog.AppendLine(
                                $"Simulation SUCCESSFUL.  Found {actionsPerThread} entries for each of the {numThreads} threads.");
                            sbLog.AppendLine($"Logging {Entries.Count} ({nameof(numThreads)} - val: {numThreads} * {nameof(actionsPerThread)} - val: {actionsPerThread} == {numThreads * actionsPerThread}) entries: ");
                            sbLog.Append(sbEntries);
                            sbLog.AppendLine();
                            sbLog.AppendLine("Logging text output: ");
                            sbLog.Append(TextResult);
                            sbLog.AppendLine();
                            sbLog.AppendLine("END TEXT LOG");
                            string timestampOnly = GetEntriesByOrderedOnlyByTimeStamp();
                            sbLog.AppendLine(timestampOnly);
                            sbLog.AppendLine();
                            sbLog.AppendLine("DONE SUCCESSFUL SIM LOGGING.");
                            report = sbLog.ToString();
                        }
                    }
                    else
                    {
                        sbEntries.Clear();
                        Entries.ApplyToAll(entry => sbEntries.AppendLine(entry.ToString()));
                        sbLog.AppendLine("SIMULATION FAILED -- MISSING TEXT FOR ONE OR MORE ENTRIES.");
                        sbLog.AppendLine("Logging all entries:");
                        sbLog.Append(sbEntries);
                        sbLog.AppendLine();
                        sbLog.AppendLine("Logging text output: ");
                        sbLog.AppendLine(TextResult);
                        sbLog.AppendLine("DONE LOGGING FAILED SIMULATION");
                        report = sbLog.ToString();
                    }
                }
                catch (Exception ex)
                {
                    success = false;
                    sbLog.AppendLine($"Error processing Entries: [{ex}]");
                    sbEntries.Clear();
                    sbEntries.AppendLine("One or more entries not found.  Listing all entries:");
                    foreach (var entry in Entries)
                    {
                        sbEntries.AppendLine(entry.ToString());
                    }

                    sbLog.Append(sbEntries);
                    sbEntries.Clear();
                    sbLog.AppendLine();
                    sbLog.AppendLine("Printing text: ");
                    sbLog.Append(TextResult);
                    report = sbLog.ToString();

                }
            }
            return (success, report);
        }

        private string GetEntriesByOrderedOnlyByTimeStamp()
        {
            StringBuilder sb = new StringBuilder();
            var entriesSortedByTs = Entries.OrderBy(entry => entry.TimeStamp).ToArray();
            sb.AppendLine("LISTING ENTRIES BY TIMESTAMP ALONE:");
            foreach (var entry in entriesSortedByTs)
            {
                sb.AppendLine(
                    $"ThreadId: [{entry.ManagedThreadId.ToString()}]\t\t\tTimestamp: [{entry.TimeStamp:O}]\t\t\tAction: [{entry.ActionCount}]");
            }

            return sb.ToString();
        }

        private bool FindMatchingText(in StressTestEntry entry, StringBuilder log)
        {
            IEnumerable<int> indices = AllIndicesOf(TextResult, entry.Text).ToArray();
            try
            {
                int singleIdx = AllIndicesOf(TextResult, entry.Text).Single();
                return true;
            }
            catch (Exception)
            {
                log.AppendLine(!indices.Any()
                    ? $"Unable to find matching output in string for entry: [{entry.ToString()}]"
                    : $"Multiple locations found in text for following entry: [{entry.ToString()}]");
                return false;
            }
            
        }

        private static IEnumerable<int> AllIndicesOf([NotNull] string str, [NotNull] string value)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(value))
                yield break;
            for (int index = 0;; index += value.Length)
            {
                index = str.IndexOf(value, index, StringComparison.Ordinal);
                if (index == -1)
                    break;
                yield return index;
            }
        }
    }

    [VaultSafe]
    readonly struct StressTestEntry : IEquatable<StressTestEntry>, IComparable<StressTestEntry>
    {
        public static StressTestEntry CreateStressTestEntry(int actionNumber)
        {
            DateTime ts = TimeStampSource.Now;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            return new StressTestEntry(ts, threadId, CreateText(actionNumber, ts, threadId), actionNumber);

            static string CreateText(int an, DateTime stamp, int ti)
                => $"<*--- Thread Id: [{ti.ToString()}], ActionNo: [{an.ToString()}], Timestamp: [{stamp:O}] ---*>";
        }

        public DateTime TimeStamp { get; }
        public int ManagedThreadId { get; }
        [NotNull] public string Text { get; }
        public int ActionCount { get; }


        private StressTestEntry(DateTime ts, int threadId,
            [NotNull] string text, int action)
        {
            TimeStamp = ts;
            ManagedThreadId = threadId;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            ActionCount = action;
        }

        public static bool operator ==(in StressTestEntry lhs, in StressTestEntry rhs) =>
            lhs.TimeStamp == rhs.TimeStamp && lhs.ManagedThreadId == rhs.ManagedThreadId &&
            string.Equals(lhs.Text, rhs.Text, StringComparison.Ordinal) && lhs.ActionCount == rhs.ActionCount;

        public static bool operator !=(in StressTestEntry lhs, in StressTestEntry rhs) => !(lhs == rhs);
        public static bool operator >(in StressTestEntry lhs, in StressTestEntry rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in StressTestEntry lhs, in StressTestEntry rhs) => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in StressTestEntry lhs, in StressTestEntry rhs) => !(lhs < rhs);
        public static bool operator <=(in StressTestEntry lhs, in StressTestEntry rhs) => !(lhs > rhs);
        public override int GetHashCode() => TimeStamp.GetHashCode();
        public int CompareTo(StressTestEntry other) => Compare(in this, in other);
        public override bool Equals(object obj) => (obj as StressTestEntry?) == this;
        public bool Equals(StressTestEntry other) => this == other;
        public override string ToString() =>
            $"Entry ts: [{TimeStamp:O}], ThreadId: [{ManagedThreadId.ToString()}], " +
            $"ActionNo: [{ActionCount.ToString()}]";
        

        private static int Compare(in StressTestEntry lhs, in StressTestEntry rhs)
        {
            int ret;
            int tsCompare = lhs.TimeStamp.CompareTo(rhs.TimeStamp);
            if (tsCompare != 0)
            {
                int threadCompare = lhs.ManagedThreadId.CompareTo(rhs.ManagedThreadId);
                if (threadCompare == 0)
                {
                    int actionCompare = lhs.ActionCount.CompareTo(rhs.ActionCount);
                    ret = actionCompare == 0
                        ? string.Compare(lhs.Text, rhs.Text, StringComparison.Ordinal)
                        : actionCompare;
                }
                else
                {
                    ret = threadCompare;
                }
            }
            else
            {
                ret = tsCompare;
            }
            return ret;
        }
        
    }
}
