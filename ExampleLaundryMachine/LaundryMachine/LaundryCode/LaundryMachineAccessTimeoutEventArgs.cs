using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class LaundryMachineAccessTimeoutEventArgs : EventArgs
    {
        public static LaundryMachineAccessTimeoutEventArgs CreateLmAccessTimeoutTimeStamp(long machineNumber,
            DateTime startAt, DateTime toAt, [NotNull] TimeoutException err, [CallerMemberName] [NotNull] string opName = "") =>
            new LaundryMachineAccessTimeoutEventArgs(startAt, opName, toAt,
                (err ?? throw new ArgumentNullException(nameof(err))).ToString(), machineNumber);

        public long LaundryMachineNumber { get; }
        public DateTime AccessAttemptStartedAtTimestamp { get; }
        [NotNull] public string OperationName { get; }
        public DateTime AccessAttemptFailedAtTimestamp { get; }
        [NotNull] public string TimeoutExceptionText { get; }
        public TimeSpan AttemptDuration => AccessAttemptFailedAtTimestamp - AccessAttemptStartedAtTimestamp;

        private LaundryMachineAccessTimeoutEventArgs(DateTime accessAttemptStart, [NotNull] string operationName,
            DateTime timeoutAt, [NotNull] string exceptionText, long laundryMachineNumber)
        {
            LaundryMachineNumber = laundryMachineNumber;
            AccessAttemptStartedAtTimestamp = accessAttemptStart;
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            AccessAttemptFailedAtTimestamp = timeoutAt;
            TimeoutExceptionText = exceptionText ?? throw new ArgumentNullException(nameof(exceptionText));
            _stringRep = new LocklessLazyWriteOnce<string>(GetStringRepresentation);
        }

        public override string ToString() => _stringRep;

        private string GetStringRepresentation() =>
            $"An attempt to perform the {OperationName} operation on Laundry Machine {LaundryMachineNumber} " +
            $"timed out.  The attempt began at [{AccessAttemptStartedAtTimestamp:O}] and timed out " +
            $"at [{AccessAttemptFailedAtTimestamp:O}] representing a delay of {AttemptDuration.TotalMilliseconds:F3} " +
            $"milliseconds.  The text of the associated exception follows: {Environment.NewLine} [{TimeoutExceptionText}]";
        

        [NotNull] private readonly LocklessLazyWriteOnce<string> _stringRep;
    }
}