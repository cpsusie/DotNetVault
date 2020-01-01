using System;
using System.Threading;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class ExceptionEventArgs : EventArgs
    {
        [NotNull] public Exception Error { get; }
        public DateTime TimeStamp { get; }
        public int ThreadId { get; }
        [NotNull] public string ThreadName { get; }
        public ExceptionEventArgs([NotNull] Exception error) : this(error, TimeStampSource.Now) { }

        public ExceptionEventArgs([NotNull] Exception error, DateTime ts)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            TimeStamp = ts;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            ThreadName = Thread.CurrentThread.Name ?? string.Empty;
        }

        public override string ToString() =>
            $"On thread (name: [{ThreadName}]) with id [{ThreadId.ToString()}] at [{TimeStamp:O}], " +
            $"exception of type [{Error.GetType().Name}] was thrown.";


    }
}