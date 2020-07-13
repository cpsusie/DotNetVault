using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Attributes;
using JetBrains.Annotations;
using ListValType = System.UInt64;
namespace VaultUnitTests
{
    using RoLckRes = DotNetVault.LockedResources.RoValListLockedResource<DotNetVault.Vaults.ReadWriteValueListVault<ListValType>, ListValType>;
    using RwLckRes = DotNetVault.LockedResources.ValListLockedResource<DotNetVault.Vaults.ReadWriteValueListVault<ListValType>, ListValType>;
    
    public interface IDualQueryOperationProvider
    {
        DualQueryOperationResult<TResult> Execute<[VaultSafeTypeParam] TResult, [VaultSafeTypeParam] TAncillary1,
            [VaultSafeTypeParam] TAncillaryTwo>(in RoLckRes lck,
            [NotNull] QueryOperation<TResult, TAncillary1, TAncillaryTwo> testQueryOp, [NotNull] Func<List<ListValType>, TResult> controlQuery,
            [NotNull] in TAncillary1 anc1, [NotNull] in TAncillaryTwo anc2) where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>;
    }

    public readonly struct DualOperationProviderFactory
    {
        [NotNull]
        public IDualQueryOperationProvider CreateDualOperationProvider([NotNull] List<ListValType> controlList) =>
            DualQueryOperationProvider.CreateDualQueryOperationProvider(controlList);
    }

    sealed class DualQueryOperationProvider : IDualQueryOperationProvider
    {
        #region Static Factory Method
        [NotNull]
        internal static IDualQueryOperationProvider
            CreateDualQueryOperationProvider([NotNull] List<ListValType> controlList) =>
            new DualQueryOperationProvider(controlList ?? throw new ArgumentNullException(nameof(controlList)));
        #endregion

        #region Implementation
        public DualQueryOperationResult<TResult> Execute<[VaultSafeTypeParam] TResult, [VaultSafeTypeParam] TAncillary1,
            [VaultSafeTypeParam] TAncillaryTwo>(in RoLckRes lck,
            QueryOperation<TResult, TAncillary1, TAncillaryTwo> testQueryOp,
            Func<List<ListValType>, TResult> controlQuery,
            in TAncillary1 anc1, in TAncillaryTwo anc2)
            where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>
        {
            if (testQueryOp == null) throw new ArgumentNullException(nameof(testQueryOp));
            if (controlQuery == null) throw new ArgumentNullException(nameof(controlQuery));
            if (anc1 == null) throw new ArgumentNullException(nameof(anc1));
            if (anc2 == null) throw new ArgumentNullException(nameof(anc2));

            var controlQueryRes = ExecuteControlQuery(controlQuery);
            var testQueryRes = ExecuteTestQuery(in lck, testQueryOp, in anc1, in anc2);
            return new DualQueryOperationResult<TResult>(in controlQueryRes, in testQueryRes);
        } 
        #endregion

        #region Private CTOR
        private DualQueryOperationProvider([NotNull] List<ListValType> controlList) => _list = controlList; 
        #endregion

        #region Private Methods
        private QueryOpResult<TResult> ExecuteTestQuery<[VaultSafeTypeParam] TResult, [VaultSafeTypeParam] TAncillary1,
            [VaultSafeTypeParam] TAncillaryTwo>(in RoLckRes lck,
            QueryOperation<TResult, TAncillary1, TAncillaryTwo> testQueryOp, in TAncillary1 anc1, in TAncillaryTwo anc2)
            where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>
        {
            Exception faulting;
            TResult res;

            try
            {
                res = testQueryOp(in lck, in anc1, in anc2);
                faulting = null;
            }
            catch (Exception ex)
            {
                faulting = ex;
                res = default;
            }

            return faulting == null
                ? QueryOpResult<TResult>.CreateTestQueryResultNoException(res)
                : QueryOpResult<TResult>.CreateTestQueryResultExceptionNoResultObj(faulting);
        }

        private QueryOpResult<TResult>
            ExecuteControlQuery<[VaultSafeTypeParam] TResult>([NotNull] Func<List<ListValType>, TResult> controlQuery)
            where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>
        {
            Exception faulting;
            TResult res;

            try
            {
                res = controlQuery(_list);
                faulting = null;
            }
            catch (Exception ex)
            {
                faulting = ex;
                res = default;
            }

            return faulting == null
                ? QueryOpResult<TResult>.CreateControlQueryResultNoException(res)
                : QueryOpResult<TResult>.CreateControlQueryResultExceptionNoResultObj(faulting);
        } 
        #endregion

        #region Private Data
        [NotNull] private readonly List<ListValType> _list; 
        #endregion
    }
}
