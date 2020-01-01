using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public class EntryInvariantsNotMetException : InvariantsNotMetException
    {
        public EntryInvariantsNotMetException(LaundryMachineStateCode stateCode, [NotNull] string invariantDescription) :
            this(stateCode, invariantDescription, TimeStampSource.Now, null) { }
        public EntryInvariantsNotMetException(LaundryMachineStateCode stateCode, [NotNull] string invariantDescription, Exception inner)
            : this(stateCode, invariantDescription, TimeStampSource.Now, inner) { }

        public EntryInvariantsNotMetException(LaundryMachineStateCode stateCode, [NotNull] string invariantDescription,
            DateTime timestamp, [CanBeNull] Exception inner) :
            base(timestamp, stateCode, invariantDescription, inner) { }

        protected sealed override string CreateMessage()
        {
            string innerExceptionString = InnerException == null
                ? string.Empty
                : $"  Inner exception message was: [{InnerException.Message}]";
            return
                $"At [{TimeStamp:O}], the {StateCode.ToString()} state failed to meet its entry invariant(s).  " +
                $"Entry invariants: [{InvariantDescription}].{innerExceptionString}";
        }
    }
}