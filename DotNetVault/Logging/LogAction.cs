using System;
using System.Threading;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    internal readonly struct LogAction : IEquatable<LogAction>, IComparable<LogAction>
    {
        public DateTime TimeStamp { get; }

        public string Text { get; }

        public int ThreadId { get; }

        public LogAction(string text)
        {
            TimeStamp = DateTime.Now;
            Text = text ?? string.Empty;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public LogAction([NotNull] Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));

            Text = ex.ToString();
            TimeStamp = DateTime.Now;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public override string ToString() => $"[{TimeStamp:O}-thread#:{ThreadId}-EntryNum#-{EntryExitLog.CurrentEntryNum}]: {Text}";

        public static bool operator ==(in LogAction lhs, in LogAction rhs) =>
            lhs.TimeStamp == rhs.TimeStamp && StringComparer.Ordinal.Equals(lhs.Text, rhs.Text);

        public static bool operator !=(in LogAction lhs, in LogAction rhs) => !(lhs == rhs);

        public override int GetHashCode() => TimeStamp.GetHashCode();

        public override bool Equals(object other) => this == other as LogAction?;

        public bool Equals(LogAction other) => this == other;

        public int CompareTo(LogAction other) => Compare(this, other);

        private static int Compare(in LogAction lhs, in LogAction rhs)
        {
            int timeComparison = lhs.TimeStamp.CompareTo(rhs.TimeStamp);
            return timeComparison == 0 ? StringComparer.Ordinal.Compare(lhs.Text, rhs.Text) : timeComparison;
        }

    }
}