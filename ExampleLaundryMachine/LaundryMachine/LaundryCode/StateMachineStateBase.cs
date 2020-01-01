using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public abstract class StateMachineStateBase<TStatusFlags, [VaultSafeTypeParam] TStateCode, [VaultSafeTypeParam] TTaskResultCode,
        [VaultSafeTypeParam] TTaskResult, [VaultSafeTypeParam] TStateType, [VaultSafeTypeParam] TStateCodeBacker, TStateTransition, TFlagVault> : IStateMachineState<TStateCode, TStateType, TStateTransition>

        where TStateCode : unmanaged, Enum
        where TTaskResultCode : unmanaged, Enum
        where TTaskResult : struct, IEquatable<TTaskResult>, IComparable<TTaskResult>
        where TStateType : unmanaged, Enum
        where TStatusFlags : class
        where TStateCodeBacker : unmanaged, IEquatable<TStateCodeBacker>, IComparable<TStateCodeBacker>
        where TStateTransition : StateTransition<TStatusFlags, TStateCode, TTaskResultCode, TTaskResult, TStateType, TStateCodeBacker, TFlagVault>
        where TFlagVault : class, IVault<TStatusFlags>
    {
        public event EventHandler<TransitionPredicateTrueEventArgs<TStateCode>> TransitionPredicateTrue;
        public event EventHandler TimedOutGettingStatusLock;
        public event EventHandler<UnexpectedExceptionDuringTransitionEventArgs> UnexpectedExceptionThrown;
        public TStateCode StateCode { get; }
        public bool IsDisposed => _isDisposed.IsSet;
        public abstract TStateType StateType { get; }

        void IStateMachineState<TStateCode, TStateType>.RaiseTransitionPredicateTrue(
            IStateTransition<TStateCode> trans)
            => RaiseTransitionPredicateTrue((TStateTransition) trans);
 
        [NotNull] protected ImmutableSortedSet<TStateTransition> StateTransitionTable => _transitionTable.Value;
        protected Type ConcreteType => _concreteType;
        protected string ConcreteTypeName => ConcreteType.Name;
        [NotNull] protected TFlagVault FlagVault { get; }

        [NotNull] protected IEventRaiser EventRaiser => _raiser;

        protected StateMachineStateBase(TStateCode code, [NotNull] TFlagVault flagVault,
             [NotNull] IEventRaiser raiser)
        {
            if (!code.IsValueDefined()) throw new ArgumentException($"The enum value [{code}] is not defined.", nameof(code));
            if (raiser == null) throw new ArgumentNullException(nameof(raiser));
            if (!raiser.ThreadActive || raiser.IsDisposed)
                throw new ArgumentException("The event raiser is disposed or faulted.", nameof(raiser));
            _raiser = raiser;
            _transitionTable = new LocklessLazyWriteOnce<ImmutableSortedSet<TStateTransition>>(InitTransitionTable);
            FlagVault = flagVault ?? throw new ArgumentNullException(nameof(flagVault));
            _concreteType = new LocklessConcreteType(this);
            StateCode = code;
        }

        public abstract void Begin();



        public virtual void ClearEvents()
        {
            TimedOutGettingStatusLock = null;
            UnexpectedExceptionThrown = null;
        }

        protected abstract (TStateCode? NextStateCode, StateMachineStateBase<TStatusFlags, TStateCode,
                TTaskResultCode,
                TTaskResult, TStateType, TStateCodeBacker, TStateTransition, TFlagVault> NextState)
             PerformFindAndExecutePossibleTransition(TimeSpan timeout);
        //{
        //    ThrowIfDisposed();
        //    try
        //    {
        //        using LockedVaultMutableResource<MutableResourceVault<TStatusFlags>, TStatusFlags> lck =
        //            _statusFlagsVault.Lock(timeout);
        //        foreach (var item in StateTransitionTable)
        //        {
        //            TStateCode? res = item.ConsiderTransition(in lck);
        //            if (res != null)
        //            {
        //                return !_comparer.Equals(res.Value, StateCode)
        //                    ? (res.Value, GetNextState(res.Value))
        //                    : (res.Value, null);
        //            }
     
        //        }
        //        return (null, null);
        //    }
        //    catch (TimeoutException)
        //    {
        //        OnTimedOutGettingStatusLock();
        //        return (null, null);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.Error.WriteLineAsync(ex.ToString());
        //        return (null, null);
        //    }
        //}

        [NotNull] protected abstract StateMachineStateBase<TStatusFlags, TStateCode, TTaskResultCode,
            TTaskResult, TStateType, TStateCodeBacker, TStateTransition, TFlagVault> PerformGetNextState(TStateCode code);
        

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public abstract void ValidateEntryInvariants();

        public abstract void EstablishExitInvariants();

        public void RaiseTransitionPredicateTrue(TStateTransition trans) => OnTransitionPredicateTrue(trans);

        protected virtual void OnTransitionPredicateTrue(TStateTransition t)
        {
            EventHandler<TransitionPredicateTrueEventArgs<TStateCode>> handler = TransitionPredicateTrue;
            if (handler != null)
            {
                bool shouldRaiseLocally = !_raiser.ThreadActive || _raiser.IsDisposed;
                if (shouldRaiseLocally)
                {
                    Action( t, TransitionPredicateTrue, this);
                }
                else
                {
                    TStateTransition temp = t;
                    _raiser.AddAction(() => Action(temp, TransitionPredicateTrue, this));
                }
            }
            
            static void  Action(TStateTransition trans, EventHandler<TransitionPredicateTrueEventArgs<TStateCode>> h, object sender)
            {
                if (h == null || sender == null) return;
                try
                {
                    TransitionPredicateTrueEventArgs<TStateCode> args  = TransitionPredicateTrueEventArgs<TStateCode>.CreateArgs(trans);
                    h(sender, args);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
#if DEBUG
                    if (Debugger.IsAttached)
                        Debugger.Break();
#endif
                }
            }
        }

        protected virtual void OnTimedOutGettingStatusLock()
        {
            bool shouldRaiseLocally = _raiser.IsDisposed || !_raiser.ThreadActive;
            if (!shouldRaiseLocally)
            {
                try
                {
                    _raiser.AddAction(Action);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                    Debug.WriteLine(ex.ToString());
                    shouldRaiseLocally = true;
                }
            }

            if (shouldRaiseLocally)
            {
                Action();
            }
            
            void Action() => TimedOutGettingStatusLock?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnUnexpectedExceptionThrown([NotNull] Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));

            bool shouldRaiseLocally = _raiser.IsDisposed || !_raiser.ThreadActive;
            if (!shouldRaiseLocally)
            {
                try
                {
                    _raiser.AddAction(Action);
                }
                catch (Exception ex2)
                {
                    Console.Error.WriteLineAsync(ex2.ToString());
                    Debug.WriteLine(ex2.ToString());
                    shouldRaiseLocally = true;
                }
            }

            if (shouldRaiseLocally)
            {
                Action();
            }

            void Action() => UnexpectedExceptionThrown?.Invoke(this,
                new UnexpectedExceptionDuringTransitionEventArgs(TimeStampSource.Now, ex));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _isDisposed.TrySet())
            {
                ClearEvents();
            }
            _isDisposed.TrySet();
        }

        protected abstract ImmutableSortedSet<TStateTransition> InitTransitionTable();

        protected void ThrowIfDisposed([CallerMemberName] string caller = "")
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(ConcreteTypeName,
                    $"Illegal call to {ConcreteTypeName}'s {caller ?? "UNKNOWN"} member: the object has been disposed.");
            }
        }

        private readonly LocklessConcreteType _concreteType;
        private readonly LocklessSetOnlyFlag _isDisposed = new LocklessSetOnlyFlag();
        private readonly LocklessLazyWriteOnce<ImmutableSortedSet<TStateTransition>> _transitionTable;
        [NotNull] private readonly IEventRaiser _raiser;
    }
}