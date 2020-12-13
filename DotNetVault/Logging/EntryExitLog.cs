using System;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.TimeStamps;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    /// <summary>
    /// Use tracelog or debug log to log method entry exit
    /// </summary>
    public readonly ref struct EntryExitLog
    {
        /// <summary>
        /// The current entry number
        /// </summary>
        public static long CurrentEntryNum => EntryId.Value;
        /// <summary>
        /// Entry exit log
        /// </summary>
        /// <param name="callingType">the calling type</param>
        /// <param name="callingMethod">the calling method</param>
        /// <param name="arr">the parameters if any</param>
        /// <returns>A disposable entry exit log</returns>
        public static EntryExitLog CreateEntryExitLog([NotNull] Type callingType,
            [NotNull] string callingMethod, params object[] arr) =>
            new EntryExitLog(false, callingType, callingMethod, DnvTimeStampProvider.MonoLocalNow, arr);

        /// <summary>
        /// Entry exit log
        /// </summary>
        /// <param name="always">true if always logged</param>
        /// <param name="callingType">the calling type</param>
        /// <param name="callingMethod">the calling method</param>
        /// <param name="arr">the parameters if any</param>
        /// <returns>A disposable entry exit log</returns>
        public static EntryExitLog CreateEntryExitLog(bool always, [NotNull] Type callingType,
            [NotNull] string callingMethod, params object[] arr) =>
            new EntryExitLog(always, callingType, callingMethod, DnvTimeStampProvider.MonoLocalNow, arr);

        private EntryExitLog(bool always, [NotNull] Type callingType, [NotNull] string callingMethod,
            DateTime openingTimeStamp, object [] arr)
        {
            
            _ts = openingTimeStamp;
            _always = always;
            _class = callingType ?? throw new ArgumentNullException(nameof(always));
            _method = callingMethod ?? throw new ArgumentNullException(nameof(callingMethod));
            _threadId = Thread.CurrentThread.ManagedThreadId;
            _entryNum = ++EntryId.Value;
            LogEntry(GetParams(arr));
        }

        /// <summary>
        /// Log Exit
        /// </summary>
        [NoDirectInvoke]
        public void Dispose()
        {
            LogExit();
        }

#if DEBUG
        void LogEntry(string paramsToLog)
        {
            if (_always)
            {
                try
                {
                    DebugLog.Log(
                        $"--->At [{_ts:O}], ENTERED thread#:id# [{_threadId}:{_entryNum}] entered, type: [{_class.Name}], method: [{_method}], params: {paramsToLog}");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }
            }
        }

        void LogExit()
        {
            if (_always)
            {
                try
                {
                    DateTime ts = DnvTimeStampProvider.MonoLocalNow;
                    DebugLog.Log(
                        $"<---At [{ts:O}], EXITED thread#:id# [{_threadId}:{_entryNum}], Duration {(ts - _ts).TotalMilliseconds:F3} milliseconds.");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }
            }
        }
#else
        void LogEntry(string paramsToLog)
        {
            if (_always)
            {
                try
                {
                    TraceLog.Log(
                        $"--->At [{_ts:O}], ENTERED thread#:id# [{_threadId}:{_entryNum}] entered, type: [{_class.Name}], method: [{_method}], params: {paramsToLog}");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync(e.ToString());
                }
            }

        }

        void LogExit()
        {
            if (_always)
            {
                if (_always)
                {
                    try
                    {
                        DateTime ts = DnvTimeStampProvider.MonoLocalNow;
                        TraceLog.Log(
                            $"<---At [{ts:O}], EXITED thread#:id# [{_threadId}:{_entryNum}], Duration {(ts - _ts).TotalMilliseconds:F3} milliseconds.");
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLineAsync(e.ToString());
                    }
                }
            }

        }

#endif

        string GetParams(params object[] param)
        {
            string ret;
            if (_always)
            {
                param ??= Array.Empty<object>();
                StringBuilder sb = new StringBuilder("(");
                foreach (var obj in param)
                {
                    sb.Append(obj?.ToString() ?? "NULL");
                    sb.Append(", ");
                }

                sb.Append(")");
                ret = sb.ToString();
            }
            else
            {
                ret = string.Empty;
            }

            return ret;
        }

        private readonly int _threadId;
        private static readonly ThreadLocal<long> EntryId = new ThreadLocal<long>(() => 0);
        private readonly long _entryNum;
        [NotNull] private readonly Type _class;
        private readonly DateTime _ts;
        [NotNull] private readonly string _method;
        private readonly bool _always;
        
    }
}