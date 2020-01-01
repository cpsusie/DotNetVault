using System;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class TransitionPredicateTrueEventArgs<[VaultSafeTypeParam] TStateCode> : EventArgs, IStateTransition<TStateCode> where TStateCode : unmanaged, Enum
    {
        public static TransitionPredicateTrueEventArgs<TStateCode> CreateArgs<TStateTransition>(TStateTransition trans)
            where TStateTransition : IStateTransition<TStateCode> =>
            new TransitionPredicateTrueEventArgs<TStateCode>(trans.OriginState, trans.Priority, trans.TransitionName, trans.PredicateText, trans.DestinationStates);

        public DateTime TimeStamp => _timeStamp;
        object IStateTransition.OriginState => OriginState;
        public ImmutableArray<TStateCode> DestinationStates => _destinationStates;
        public TStateCode OriginState => _originState;
        public int Priority => _priority;
        public string TransitionName => _transitionName;
        public string PredicateText => _predicateText;

        private TransitionPredicateTrueEventArgs(TStateCode origin, int priority, [NotNull] string name, [NotNull] string predText,
            ImmutableArray<TStateCode> destinations)
        {
            _timeStamp = TimeStampSource.Now;
            _priority = priority;
            _originState = origin;
            _predicateText = predText ?? throw new ArgumentNullException(nameof(predText));
            _destinationStates = destinations;
            _transitionName = name ?? throw new ArgumentNullException(nameof(predText));
            _stringRep = new LocklessLazyWriteOnce<string>(GetStringRep);
        }

        public override string ToString() => _stringRep;
        

        private string GetStringRep()
            => $"At [{TimeStamp:O}], the predicate " +
               $"[{PredicateText}] of the transition named [{TransitionName}] with priority [{Priority.ToString()}] evaluated to true.  " +
               $"The transition will be processed and a state change MAY follow";
        

        [NotNull] private readonly LocklessLazyWriteOnce<string> _stringRep;
        private DateTime _timeStamp;
        private int _priority;
        [NotNull] private string _transitionName;
        [NotNull] private string _predicateText;
        private TStateCode _originState;
        private ImmutableArray<TStateCode> _destinationStates;
       
    }
}