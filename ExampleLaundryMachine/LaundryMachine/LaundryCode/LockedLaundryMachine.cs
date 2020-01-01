using System;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public readonly ref struct LockedLaundryMachine
    {
        internal static LockedLaundryMachine CreateLockedResource(
            LockedVaultMutableResource<MutableResourceVault<LaundryMachine>, LaundryMachine> me) =>
            new LockedLaundryMachine(me);



        public LaundryMachineStateCode StateCode => ExecuteQuery((in LaundryMachine lm) =>
            lm.QueryExactStateCode(TimeSpan.FromMilliseconds(250)) ??
            LaundryMachineStateCode.Error);

        public long MachineId => ExecuteQuery((in LaundryMachine lm) => lm.MachineId);

        public bool TurnOnMachine() => ExecuteMixedOperation((ref LaundryMachine lm) => lm.TurnOnMachine());

        public bool TurnOffMachine() => ExecuteMixedOperation((ref LaundryMachine lm) => lm.TurnOffMachine());

        public Guid? LoadLaundry(in LaundryItems laundry) =>
            ExecuteMixedOperation((ref LaundryMachine lm, in LaundryItems l) => lm.LoadLaundry(in l), in laundry);

        public (Guid? LoadedLaundry, bool Cycled) LoadAndCycle(in LaundryItems laundry) =>
            ExecuteMixedOperation((ref LaundryMachine lm, in LaundryItems l) => lm.LoadAndCycle(in l), in laundry);

        public LaundryItems? UnloadLaundry(in Guid id) =>
            ExecuteMixedOperation((ref LaundryMachine lm, in Guid i) => lm.UnloadLaundry(in i), in id);

        public LaundryItems? UnloadAnyLaundry() =>
            ExecuteMixedOperation((ref LaundryMachine lm) => lm.UnloadAnyLaundry());
        public void DisposeLaundryMachine() => ExecuteAction((ref LaundryMachine lm) => lm?.Dispose());
        public bool InitiateWash() => ExecuteMixedOperation((ref LaundryMachine lm) => lm.InitiateWash());

        public bool InitiateDry() => ExecuteMixedOperation((ref LaundryMachine lm) => lm.InitiateDry());

        public bool InitiateWashDry() => ExecuteMixedOperation((ref LaundryMachine lm) => lm.InitiateWashDry());

        public bool Abort() => ExecuteMixedOperation((ref LaundryMachine lm) => lm.Abort());

        #region CTOR
        private LockedLaundryMachine(
            LockedVaultMutableResource<MutableResourceVault<LaundryMachine>,
                LaundryMachine> wrappedLockedResource) =>
            _resource = wrappedLockedResource;
        #endregion

        public void Dispose() => _resource.Dispose();

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
            [NotNull] VaultQuery<LaundryMachine, TAncillary, TResult> q, in TAncillary val) =>
            _resource.ExecuteQuery(q, in val);
        /// <summary>
        /// Execute a query on the mutable resource
        /// </summary>
        /// <typeparam name="TResult">the result of the query</typeparam>
        /// <param name="q">the query delegate</param>
        /// <returns>the result of the delegate execution.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteQuery<[VaultSafeTypeParam] TResult>([NotNull]
            VaultQuery<LaundryMachine, TResult> q) => _resource.ExecuteQuery(q);
        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <param name="action">the action</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction([NotNull] VaultAction<LaundryMachine> action)
            => _resource.ExecuteAction(action);
        /// <summary>
        /// Perform a mutation on the protected resource.
        /// </summary>
        /// <typeparam name="TAncillary">an ancillary to be used by the mutation delegate</typeparam>
        /// <param name="action">the mutation action</param>
        /// <param name="ancillaryValue">the ancillary value</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ExecuteAction<[VaultSafeTypeParam] TAncillary>(
            [NotNull] VaultAction<LaundryMachine, TAncillary> action, in TAncillary ancillaryValue)
            => _resource.ExecuteAction(action, in ancillaryValue);
        /// <summary>
        /// Executes a query on the mutable resource, while potentially performing a mutation on it.
        /// </summary>
        /// <typeparam name="TResult">The result object</typeparam>
        /// <param name="mixedOp">The mixed query/mutate delegate</param>
        /// <returns>the result</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public TResult ExecuteMixedOperation<[VaultSafeTypeParam] TResult>(
            [NotNull] VaultMixedOperation<LaundryMachine, TResult> mixedOp) =>
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
            [NotNull] VaultMixedOperation<LaundryMachine, TAncillary, TResult> mixedOp, in TAncillary ancillary)
            => _resource.ExecuteMixedOperation(mixedOp, in ancillary);
        #endregion

        private readonly LockedVaultMutableResource<MutableResourceVault<LaundryMachine>, LaundryMachine>
            _resource;
    }
}