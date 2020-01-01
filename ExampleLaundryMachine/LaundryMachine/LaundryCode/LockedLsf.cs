using System;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public readonly ref struct LockedLsf
    {
        internal static LockedLsf CreateLockedResource(
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> me)
            => new LockedLsf(me);

        public Guid? LoadedLaundryId => _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.LoadedLaundryId);
        public string LoadedLaundryDescription =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.LoadedLaundryDescription);
        public bool IsEmpty => _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.IsEmpty);
        public LaundryItems LoadedLaundryItem =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.LoadedLaundry);
        public CancelFlag CancelFlag => _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.CancelFlag);
        public CommandRequestStatus? CurrentCommand =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.CurrentCommand);
        public CommandRequestStatus? ActivationCommandStatus =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.ActivationCommandStatus);
        public CommandRequestStatus? ShutdownCommandStatus =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.ShutdownCommandStatus);
        public CommandRequestStatus? WashCommandRequestStatus =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.WashCommandRequestStatus);
        public CommandRequestStatus? DryCommandRequestStatus =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.DryCommandStatus);
        public ErrorRegistrationStatus ErrorRegistrationStatus =>
            _resource.ExecuteQuery((in LaundryStatusFlags lsf) => lsf.ErrorRegistrationStatus);
        public (byte OldDampness, byte NewDampness)? SoakLaundry() =>
            _resource.ExecuteMixedOperation((ref LaundryStatusFlags lsf) => lsf.SoakLoadedLaundry());
        
        #region CTOR
        private LockedLsf(
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, 
                LaundryStatusFlags> wrappedLockedResource) =>
            _resource = wrappedLockedResource;
        #endregion

        #region Query Methods
        [Pure]
        public CommandRequestStatus? FindCommandById(in CommandIds? commandIds)
            => ExecuteQuery((in LaundryStatusFlags lsf, in CommandIds? cis) => lsf.FindCommandById(in cis),
                in commandIds);
        #endregion

        #region Mutating Methods
        public void SetSoilFactor(byte soilFactor) => _resource.ExecuteAction(
            (ref LaundryStatusFlags lsf, in byte factor) =>
            {
                bool temp = lsf.SetSoiledFactor(factor);
                if (!temp) throw new InvalidOperationException("No laundry present.");
            }, soilFactor);

        public void SetDampFactor(byte newSetting) =>
            _resource.ExecuteAction((ref LaundryStatusFlags lsf, in byte damp) =>
            {
                bool temp = lsf.SetDampnessFactor(damp);
                if (!temp) throw new InvalidOperationException("No laundry present.");
            }, newSetting);
        public Guid? LoadLaundry(LaundryItems item)
            => ExecuteMixedOperation((ref LaundryStatusFlags lsf, in LaundryItems itm) => lsf.LoadLaundry(in itm),
                in item);
        public LaundryItems? UnloadLaundry(Guid identifier) =>
            ExecuteMixedOperation((ref LaundryStatusFlags lsf, in Guid id) => lsf.UnloadLaundry(id), in identifier);
        public LaundryItems? UnloadLaundry() =>
            ExecuteMixedOperation((ref LaundryStatusFlags lsf) => lsf.UnloadAnyLaundry());
        public bool AcknowledgeMyTask(in CommandIds myCommandId) => ExecuteMixedOperation(
            (ref LaundryStatusFlags lsf, in CommandIds ids) =>
            {
                ref CommandRequestStatus foundCommand = ref ((IFindCommandRequestStatusRef) lsf).FindCommandRequestStatusById(ids);
                if (foundCommand.StatusCode != CommandRequestStatusCode.Requested)
                {
                    return false;
                }

                foundCommand = foundCommand.AsPending(TimeStampSource.Now);
                return true;
            }, myCommandId);

        public bool RegisterCancelCurrentTask()
            => ExecuteMixedOperation((ref LaundryStatusFlags lsf) => lsf.RegisterCancelCurrentTask());

        public bool RegisterCancellationPending()
            => ExecuteMixedOperation((ref LaundryStatusFlags lsf) => lsf.RegisterCancellationPending());

        public bool RegisterCancellationComplete()
            => ExecuteMixedOperation((ref LaundryStatusFlags lsf) => lsf.RegisterCancellationComplete());

        public bool ResetCancellation()
            => ExecuteMixedOperation((ref LaundryStatusFlags lsf) => lsf.ResetCancellation());

        public bool RegisterWashCommand()
        {
            try
            {
                return ExecuteMixedOperation((ref LaundryStatusFlags lsf) =>
                {
                    if (lsf.IsEmpty) return false;
                    if (lsf.ActivationCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.CurrentCommand != null ||
                        lsf.ShutdownCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.WashCommandRequestStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.DryCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.ErrorRegistrationStatus.StatusCode != ErrorRegistrationStatusCode.Nil ||
                        lsf.CancelFlag.State != CancelState.Nil)
                        return false;

                    ref var washCommand =
                        ref ((IFindCommandRequestStatusRef) lsf).FindCommandRequestStatusById(CommandIds.Wash);
                    washCommand = washCommand.AsRequested(TimeStampSource.Now);
                    ref var currentCommand = ref lsf.RefToCurrentCommand;
                    currentCommand = CommandIds.Wash;
                    return true;
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                TerminationHelper.TerminateApplication("Unable to register the wash command.", ex);
                return false;
            }
        }

        public bool RegisterDryCommand() 
        {
            try
            {
                return ExecuteMixedOperation((ref LaundryStatusFlags lsf) =>
                {
                    if (lsf.IsEmpty) return false;
                    if (lsf.ActivationCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.CurrentCommand != null ||
                        lsf.ShutdownCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.WashCommandRequestStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.DryCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.ErrorRegistrationStatus.StatusCode != ErrorRegistrationStatusCode.Nil ||
                        lsf.CancelFlag.State != CancelState.Nil)
                        return false;

                    ref var dryCommand =
                        ref ((IFindCommandRequestStatusRef) lsf).FindCommandRequestStatusById(CommandIds.Dry);
                    dryCommand = dryCommand.AsRequested(TimeStampSource.Now);
                    ref var currentCommand = ref lsf.RefToCurrentCommand;
                    currentCommand = CommandIds.Dry;
                    return true;
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                TerminationHelper.TerminateApplication("Unable to register the dry command.", ex);
                return false;
            }
        }

        public bool RegisterWashDryCommand() 
        {
            try
            {
                return ExecuteMixedOperation((ref LaundryStatusFlags lsf) =>
                {
                    if (lsf.IsEmpty) return false;
                    if (lsf.ActivationCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.CurrentCommand != null ||
                        lsf.ShutdownCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.WashCommandRequestStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.DryCommandStatus.StatusCode != CommandRequestStatusCode.Nil ||
                        lsf.ErrorRegistrationStatus.StatusCode != ErrorRegistrationStatusCode.Nil ||
                        lsf.CancelFlag.State != CancelState.Nil)
                        return false;

                    ref var washCommand =
                        ref ((IFindCommandRequestStatusRef) lsf).FindCommandRequestStatusById(CommandIds.Wash);
                    ref var dryCommand =
                        ref ((IFindCommandRequestStatusRef) lsf).FindCommandRequestStatusById(CommandIds.Dry);
                    ref var currentCommand = ref lsf.RefToCurrentCommand;
                    washCommand = washCommand.AsRequested(TimeStampSource.Now);
                    dryCommand = dryCommand.AsRequested(TimeStampSource.Now);
                    currentCommand = CommandIds.Wash;
                    return true;
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLineAsync(ex.ToString());
                TerminationHelper.TerminateApplication("Unable to register the wash/dry command.", ex);
                return false;
            }
        }
            
         public bool RegisterPowerOnCommand(LaundryMachineStateCode stateCode)
            => ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf, in LaundryMachineStateCode sc) => lsf.RegisterPowerOnCommand(sc),
                stateCode);

        public bool AcknowledgePowerUpPending(LaundryMachineStateCode stateCode)
            =>
                ExecuteMixedOperation(
                    (ref LaundryStatusFlags lsf, in LaundryMachineStateCode sc) => lsf.RegisterPowerOnCommand(sc),
                    stateCode);


        public bool SignalPowerUpRefused()
            =>
                ExecuteMixedOperation(
                    (ref LaundryStatusFlags lsf) => lsf.SignalPowerUpRefused());

        public bool SignalPowerUpComplete(DateTime? ts = null, bool andReset = true) =>
            ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf, in DateTime? stamp) => lsf.SignalPowerUpComplete(ts, andReset), ts);

        public bool SignalPowerUpCancelled() =>
            ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf) => lsf.SignalPowerUpCancelled());

        public bool SignalPowerUpFaulted([NotNull] string explanation)
            => ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf, in string expl) => lsf.SignalPowerUpFaulted(expl, null), explanation);

        public bool SignalPowerUpReset() =>
            ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf) => lsf.SignalPowerUpReset());

        public void ForceClearPowerDownStatus() =>
            ExecuteAction((ref LaundryStatusFlags lsf) => lsf.ForceClearPowerDownCommand());
        public bool RegisterError(string explanation, bool isLogicError)
            => ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf, in string expl) => lsf.RegisterError(expl, isLogicError), explanation);
        public void ForceClearDryCommandAndAnyCurrentCommand() => ExecuteAction((ref LaundryStatusFlags lsf) =>
            lsf.ForceClearDryStatusAndAnyCurrentCommandIfDry());
        public bool RegisterShutdownCommand(
            in LockedVaultObject<BasicVault<LaundryMachineStateCode>, LaundryMachineStateCode> value)
            => ExecuteMixedOperation((ref LaundryStatusFlags lsf, in LaundryMachineStateCode c) =>
            {
                if (c == LaundryMachineStateCode.Empty || c == LaundryMachineStateCode.Full)
                {
                    ref CommandRequestStatus x =
                        ref ((IFindCommandRequestStatusRef) lsf).FindCommandRequestStatusById(CommandIds.Shutdown);
                    if (x.StatusCode == CommandRequestStatusCode.Nil)
                    {
                        x = x.AsRequested(TimeStampSource.Now);
                        return true;
                    }
                }

                return false;
            }, value.Value);

        public void SetCurrentCommandToSpecified(CommandIds id) =>
            ExecuteAction((ref LaundryStatusFlags lsf, in CommandIds com) =>
            {
                ref CommandRequestStatus stat =
                    ref ((IFindCommandRequestStatusRef)lsf).FindCommandRequestStatusById(com);
                if (stat.StatusCode == CommandRequestStatusCode.Nil)
                {
                    throw new StateLogicErrorException("Cannot set a status to current if corresponding flag is nil.");
                }
                lsf.RefToCurrentCommand = com;
            }, in id);
        public void CancelMyStatus(CommandIds commandId, [NotNull] TaskResultEndedEventArgs args)
       => ExecuteAction((ref LaundryStatusFlags lsf, in CommandIds ids) =>
       {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (args == null) throw new ArgumentNullException(nameof(args));
           ref CommandRequestStatus stat =
               ref ((IFindCommandRequestStatusRef)lsf).FindCommandRequestStatusById(ids);
           if (stat != lsf.CurrentCommand)
               throw new StateLogicErrorException("Attempt to cancel task that is not the active task.");
           stat = stat.AsCancelled(args.Result.TerminationTimeStamp ?? TimeStampSource.Now, args.Result.Explanation);
       }, commandId);


        public void FailMyStatus(CommandIds commandId, [NotNull] TaskResultEndedEventArgs args)
            => ExecuteAction((ref LaundryStatusFlags lsf, in CommandIds ids) =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (args == null) throw new ArgumentNullException(nameof(args));
                ref CommandRequestStatus stat =
                    ref ((IFindCommandRequestStatusRef)lsf).FindCommandRequestStatusById(ids);
                if (stat != lsf.CurrentCommand)
                    throw new StateLogicErrorException("Attempt to fail task that is not the active task.");
                stat = stat.AsFaultedNoException(args.Result.TerminationTimeStamp ?? TimeStampSource.Now, args.Result.Explanation);
            }, commandId);

        public void CompleteMyStatus(CommandIds commandId, [NotNull] TaskResultEndedEventArgs args)
            => ExecuteAction((ref LaundryStatusFlags lsf, in CommandIds ids) =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (args == null) throw new ArgumentNullException(nameof(args));
                ref CommandRequestStatus stat =
                    ref ((IFindCommandRequestStatusRef)lsf).FindCommandRequestStatusById(ids);
                if (stat != lsf.CurrentCommand)
                    throw new StateLogicErrorException("Attempt to complete task that is not the active task.");
                stat = stat.AsRanToCompletion(args.Result.TerminationTimeStamp ?? TimeStampSource.Now);
            }, commandId);
       
        public void ResetMyStatusAndAnyCancellation(CommandIds commandIds) =>
            ExecuteAction((ref LaundryStatusFlags lsf, in CommandIds ci) =>
            {
                ref CommandRequestStatus stat = ref ((IFindCommandRequestStatusRef)lsf).FindCommandRequestStatusById(ci);
                stat = stat.AsReset();
                lsf.ClearCurrentCommandIfMatch(stat.CommandId);
                lsf.ResetCancellation();
            }, commandIds);
        
        public bool ProcessError() =>
            ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf) => lsf.ProcessError());
        public bool ClearError() =>
            ExecuteMixedOperation(
                (ref LaundryStatusFlags lsf) => lsf.ClearError());
        public void ResetError()
            => ExecuteAction((ref LaundryStatusFlags lsf) => lsf.ResetError()); 
        #endregion

        #region Dispose
        public void Dispose() => _resource.Dispose();
        #endregion

        #region Wrapped Accessors for access via delegates
        /// <summary>
        /// Execute a query on the mutable resource
        /// </summary>
        /// <typeparam name="TAncillary">an ancillary value to be used in the query</typeparam>
        /// <typeparam name="TResult">the result of the query</typeparam>
        /// <param name="q">the query delegate</param>
        /// <param name="val">the ancillary value</param>
        /// <returns>the result of the delegate execution.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteQuery<[VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
            [NotNull] VaultQuery<LaundryStatusFlags, TAncillary, TResult> q, in TAncillary val) =>
            _resource.ExecuteQuery(q, in val);
        /// <summary>
        /// Execute a query on the mutable resource
        /// </summary>
        /// <typeparam name="TResult">the result of the query</typeparam>
        /// <param name="q">the query delegate</param>
        /// <returns>the result of the delegate execution.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteQuery<[VaultSafeTypeParam] TResult>([NotNull]
            VaultQuery<LaundryStatusFlags, TResult> q) => _resource.ExecuteQuery(q);
        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <param name="action">the action</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction([NotNull] VaultAction<LaundryStatusFlags> action)
            => _resource.ExecuteAction(action);
        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <typeparam name="TAncillary">an ancillary to be used by the mutation delegate</typeparam>
        /// <param name="action">the mutation action</param>
        /// <param name="ancillaryValue">the ancillary value</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction<[VaultSafeTypeParam] TAncillary>(
            [NotNull] VaultAction<LaundryStatusFlags, TAncillary> action, in TAncillary ancillaryValue)
            => _resource.ExecuteAction(action, in ancillaryValue);
        /// <summary>
        /// Executes a query on the mutable resource, while potentially performing a mutation on it.
        /// </summary>
        /// <typeparam name="TResult">The result object</typeparam>
        /// <param name="mixedOp">The mixed query/mutate delegate</param>
        /// <returns>the result</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteMixedOperation<[VaultSafeTypeParam] TResult>(
            [NotNull] VaultMixedOperation<LaundryStatusFlags, TResult> mixedOp) =>
            _resource.ExecuteMixedOperation(mixedOp);
        /// <summary>
        /// Executes a query on the mutable resource, while potentially performing a mutation on it.
        /// </summary>
        /// <typeparam name="TResult">The result object</typeparam>
        /// <typeparam name="TAncillary">the ancillary object type that should be used by the delegate</typeparam>
        /// <param name="mixedOp">The mixed query/mutate delegate</param>
        /// <param name="ancillary">the ancillary object</param>
        /// <returns>the result</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteMixedOperation<[VaultSafeTypeParam] TAncillary, [VaultSafeTypeParam] TResult>(
            [NotNull] VaultMixedOperation<LaundryStatusFlags, TAncillary, TResult> mixedOp, in TAncillary ancillary)
            => _resource.ExecuteMixedOperation(mixedOp, in ancillary);
        #endregion

        #region Privates
        private readonly LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags>
            _resource; 
        #endregion
    }
}