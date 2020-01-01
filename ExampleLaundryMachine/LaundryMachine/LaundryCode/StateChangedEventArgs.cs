using System;
using DotNetVault.Attributes;

namespace LaundryMachine.LaundryCode
{
    public sealed class StateChangedEventArgs<[VaultSafeTypeParam] TStateCode> : EventArgs where TStateCode : unmanaged, Enum
    {
        public DateTime TimeStamp { get; }
        public TStateCode OldState { get; }
        public TStateCode NewState { get; }
        public ulong StateChangeCount { get; }

        public StateChangedEventArgs(TStateCode oldState, TStateCode newState, ulong stateChangeCount, DateTime? ts = null)
        {
            TimeStamp = ts ?? TimeStampSource.Now;
            OldState = oldState;
            NewState = newState;
            StateChangeCount = stateChangeCount;
            _stringRep = new LocklessLazyWriteOnce<string>(() =>
                $"At [{TimeStamp:O}], the state (change# {StateChangeCount}) changed from [{OldState.ToString()}] to [{NewState.ToString()}].");
        }

        public override string ToString() => _stringRep;
        

        private readonly LocklessLazyWriteOnce<string> _stringRep;
    }
}