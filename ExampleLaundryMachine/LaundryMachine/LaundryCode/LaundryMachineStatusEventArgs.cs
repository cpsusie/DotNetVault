using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class LaundryMachineStatusEventArgs : EventArgs
    {
        public static LaundryMachineStatusEventArgs CreateStatusEventArgs<TEventArgs>([NotNull] string eventName,
            [NotNull] TEventArgs args) where TEventArgs : EventArgs =>
            CreateStatusEventArgs(eventName, args, TimeStampSource.Now);

        public static LaundryMachineStatusEventArgs CreateStatusEventArgs<TEventArgs>([NotNull] string eventName,
            [NotNull] TEventArgs args, DateTime receivedTimestamp) where TEventArgs : EventArgs
        {
            if (eventName == null) throw new ArgumentNullException(nameof(eventName));
            if (args == null) throw new ArgumentNullException(nameof(args));
            return new LaundryMachineStatusEventArgs(receivedTimestamp, eventName, args.ToString());
        }

        public DateTime Timestamp { get; }
        [NotNull] public string ArgContent { get; }
        [NotNull] public string EventName { get; }
        
        private LaundryMachineStatusEventArgs(DateTime receivedAt, string eventName, [NotNull] string argContent)
        {
            Timestamp = receivedAt;
            ArgContent = argContent ?? throw new ArgumentNullException(nameof(argContent));
            EventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
        }

        public override string ToString() =>
            $"At [{Timestamp:O}] event [{EventName}] occured with arguments [{ArgContent}]";
    }
}