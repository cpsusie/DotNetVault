using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public sealed class DelegateExceptionEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; }
        public string DelegateName { get; }

        public string ExceptionType { get; }

        public string ExceptionMessage { get; }

        public string ExceptionContents { get; }

        public DelegateExceptionEventArgs([NotNull] string delegateName, [NotNull] Exception thrown)
        {
            if (thrown == null) throw new ArgumentNullException(nameof(thrown));
            TimeStamp = TimeStampSource.Now;
            DelegateName = delegateName ?? throw new ArgumentNullException(nameof(delegateName));
            ExceptionType = thrown.GetType().Name;
            ExceptionMessage = thrown.Message;
            ExceptionContents = thrown.ToString();
        }

        public override string ToString() =>
            $"At [{TimeStamp:O}] delegate name [{DelegateName}] threw exception of type [{ExceptionType}]" +
            $" with message: [{ExceptionMessage}]. {Environment.NewLine} Exception contents: [{ExceptionContents}].";

    }
}