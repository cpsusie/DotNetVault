using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public sealed class UnexpectedExceptionDuringTransitionEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; }

        [NotNull] public string ExceptionContents { get; }

        public UnexpectedExceptionDuringTransitionEventArgs(DateTime ts, [NotNull] Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            TimeStamp = ts;
            ExceptionContents = ex.ToString();
        }

        public override string ToString() =>
            $"At [{TimeStamp:O}], unexpected exception thrown looking for or executing transition.  Exception contents:{Environment.NewLine}{ExceptionContents}";
    }

    [VaultSafe]
    public sealed class UnexpectedStateMachineFaultEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; }

        [NotNull] public string ExceptionContents { get; }

        public UnexpectedStateMachineFaultEventArgs(DateTime ts, [NotNull] Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            TimeStamp = ts;
            ExceptionContents = ex.ToString();
        }

        public override string ToString() =>
            $"At [{TimeStamp:O}], unexpected exception thrown faulting state machine.  Exception contents:{Environment.NewLine}{ExceptionContents}";
    }
}