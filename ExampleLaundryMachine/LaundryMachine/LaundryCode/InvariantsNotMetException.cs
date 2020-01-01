using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public abstract class InvariantsNotMetException : Exception
    {
        public DateTime TimeStamp { get; }
        public LaundryMachineStateCode StateCode { get; }
        public string InvariantDescription { get; }
        public sealed override string Message => _message.Value;

        protected InvariantsNotMetException(DateTime ts, LaundryMachineStateCode stateCode, [NotNull] string invariantDescription,
            [CanBeNull] Exception inner) : base(string.Empty, inner)
        {
            _message = new LocklessLazyWriteOnce<string>(CreateMessage);
            TimeStamp = ts;
            StateCode = stateCode;
            InvariantDescription = invariantDescription ?? throw new ArgumentNullException(nameof(invariantDescription));
        }

        [NotNull] protected abstract string CreateMessage();

        private readonly LocklessLazyWriteOnce<string> _message;

    }
}