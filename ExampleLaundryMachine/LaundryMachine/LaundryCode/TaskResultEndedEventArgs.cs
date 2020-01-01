using System;
using DotNetVault.Attributes;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public sealed class TaskResultEndedEventArgs : EventArgs
    {
        public TaskResult Result { get; }

        public TaskResultEndedEventArgs(in TaskResult r)
        {
            Result = r;
        }

        public override string ToString() => $"[{typeof(TaskResultEndedEventArgs).Name}]-- RESULT: {Result}";
    }
}