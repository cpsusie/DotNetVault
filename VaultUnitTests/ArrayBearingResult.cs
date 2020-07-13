using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DotNetVault.Attributes;
using DotNetVault.RefReturningCollections;
using JetBrains.Annotations;
using RoLckRes = DotNetVault.LockedResources.RoValListLockedResource<DotNetVault.Vaults.ReadWriteValueListVault<ulong>, ulong>;
using RwLckRes = DotNetVault.LockedResources.ValListLockedResource<DotNetVault.Vaults.ReadWriteValueListVault<ulong>, ulong>;
namespace VaultUnitTests
{
    [NoNonVsCapture]
    public delegate TResult QueryOperation<[VaultSafeTypeParam] TResult, [VaultSafeTypeParam] TAncillary1,
        [VaultSafeTypeParam] TAncillary2>(in RoLckRes lck, in TAncillary1 ancillary1, in TAncillary2 ancillary2)
        where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>;
    
    public delegate ImmutableArray<TResult> QueryOperation<[VaultSafeTypeParam] TResult>(in RoLckRes lck) where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>;

    public delegate ImmutableArray<TResult> FilteredQueryOperation<[VaultSafeTypeParam] TResult>(in RoLckRes lck,
        [NotNull] RefPredicate<ulong> predicateFilter) where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>;

    public delegate ImmutableArray<TTransformedTo> TransformingQueryOperation<[VaultSafeTypeParam] TResult,
        [VaultSafeTypeParam] TTransformedTo>(in RoLckRes lck,
        [NotNull] RefFunc<TResult, TTransformedTo> transformation)
        where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>
        where TTransformedTo : unmanaged, IEquatable<TTransformedTo>, IComparable<TTransformedTo>;

    public delegate ImmutableArray<TTransformedTo> FilteredTransformingQueryOperation<[VaultSafeTypeParam] TResult,
        [VaultSafeTypeParam] TTransformedTo>(in RoLckRes lck, [NotNull] RefPredicate<ulong> predicateFilter,
        [NotNull] RefFunc<TResult, TTransformedTo> transformation)
        where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>
        where TTransformedTo : unmanaged, IEquatable<TTransformedTo>, IComparable<TTransformedTo>;
    

    [NoNonVsCapture]
    public delegate ImmutableArray<ulong> ArrayBearingOperation<[VaultSafeTypeParam] TAncillary1,
        [VaultSafeTypeParam] TAncillary2>(ref RwLckRes lck, in TAncillary1 anc1, in TAncillary2 anc2);

    
    public readonly struct ArrayBearingResult<[VaultSafeTypeParam] T> : IEquatable<ArrayBearingResult<T>> where T : unmanaged, IEquatable<T>, IComparable<T>
    {
        #region Test Result Factories
        public static ArrayBearingResult<T> CreateTestResultExceptionNoResultArr([NotNull] Exception faultingException) => new ArrayBearingResult<T>(true, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), null);
        public static ArrayBearingResult<T> CreateTestResultExceptionAndResultArr([NotNull] Exception faultingException, ImmutableArray<T> arr) => new ArrayBearingResult<T>(true, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), arr);
        public static ArrayBearingResult<T> CreateTestResultNoException(ImmutableArray<T> arr) => new ArrayBearingResult<T>(true, null, arr);
        #endregion

        #region Control Result Factories
        public static ArrayBearingResult<T> CreateControlResultExceptionNoResultArr([NotNull] Exception faultingException) => new ArrayBearingResult<T>(false, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), null);
        public static ArrayBearingResult<T> CreateControlResultExceptionAndResultArr([NotNull] Exception faultingException, ImmutableArray<T> arr) => new ArrayBearingResult<T>(false, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), arr);
        public static ArrayBearingResult<T> CreateControlResultNoException(ImmutableArray<T> arr) => new ArrayBearingResult<T>(false, null, arr);
        #endregion

        public bool IsInvalidDefault => ResultId == default;
        public readonly Guid ResultId;
        public bool HasArrayResult => !_resArr.IsDefault;
        public readonly bool IsTest;
        public bool IsControl => !IsTest;
        [CanBeNull] public readonly Exception FaultingException;
        public ImmutableArray<T> ResultingArray => _resArr.IsDefault ? ImmutableArray<T>.Empty : _resArr;

        public static bool operator ==(in ArrayBearingResult<T> lhs, in ArrayBearingResult<T> rhs)
        {
            bool basicsEqual = lhs.ResultId == rhs.ResultId && lhs.HasArrayResult == rhs.HasArrayResult &&
                               lhs.IsTest == rhs.IsTest && lhs.FaultingException == rhs.FaultingException;
            return basicsEqual && (!lhs.HasArrayResult || lhs.ResultingArray.SequenceEqual(rhs.ResultingArray));
        }

        public static bool operator !=(in ArrayBearingResult<T> lhs, in ArrayBearingResult<T> rhs) => !(lhs == rhs);
        public override int GetHashCode() => ResultId.GetHashCode();
        public override bool Equals(object obj) => obj is ArrayBearingResult<T> res && res == this;
        public bool Equals(ArrayBearingResult<T> other) => other == this;
        public override string ToString() => GetStringRep();

        private ArrayBearingResult(bool isTest, Exception faultingException, ImmutableArray<T>? resArr)
        {
            IsTest = isTest;
            FaultingException = faultingException;
            if (true == resArr?.IsDefault) throw new ArgumentException("To avoid confusion, signify no array result by passing null.  DO NOT PASS an uninitialized default val of ImmutArr struct.");
            _resArr = resArr ?? default;
            ResultId = Guid.NewGuid();
            _faultingExType = faultingException?.GetType();
        }

        private string GetStringRep()
        {
            string ret;
            if (IsInvalidDefault)
            {
                ret = "INVALID DEFAULT RESULT";
            }
            else
            {
                ret = IsTest ? "TEST RESULT" : "CONTROL RESULT";
                ret += ((_faultingExType == null)
                    ? "\t\tNo Exception Thrown."
                    : ("\t\tException Thrown Of Type - [" + _faultingExType.Name + "]."));
                ret += HasArrayResult
                    ? ("\t\tHas result array with [" + ResultingArray.Length + "] elements.")
                    : "No array resulted.";
            }
            return ret;
        }

        [CanBeNull] private readonly Type _faultingExType;
        private readonly ImmutableArray<T> _resArr;
    }

    public readonly struct QueryOpResult<[VaultSafeTypeParam] T> : IEquatable<QueryOpResult<T>> where T : unmanaged, IEquatable<T>, IComparable<T>
    {
        #region Test Result Factories
        public static QueryOpResult<T> CreateTestQueryResultExceptionNoResultObj([NotNull] Exception faultingException) => new QueryOpResult<T>(true, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), null);
        public static QueryOpResult<T> CreateTestQueryResultExceptionAndResultObj([NotNull] Exception faultingException, T queryRes) => new QueryOpResult<T>(true, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), queryRes);
        public static QueryOpResult<T> CreateTestQueryResultNoException(T queryRes) => new QueryOpResult<T>(true, null, queryRes);
        #endregion

        #region Control Result Factories
        public static QueryOpResult<T> CreateControlQueryResultExceptionNoResultObj([NotNull] Exception faultingException) => new QueryOpResult<T>(false, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), null);
        public static QueryOpResult<T> CreateControlQueryResultExceptionAndResultObj([NotNull] Exception faultingException, T queryRes) => new QueryOpResult<T>(false, faultingException ?? throw new ArgumentNullException(nameof(faultingException)), queryRes);
        public static QueryOpResult<T> CreateControlQueryResultNoException(T queryRes) => new QueryOpResult<T>(false, null, queryRes);
        #endregion

        public bool IsInvalidDefault => ResultId == default;
        public readonly Guid ResultId;
        public bool HasQueryRes => QueryResult != null;
        public readonly bool IsTest;
        public bool IsControl => !IsTest;
        [CanBeNull] public readonly Exception FaultingException;
        public readonly T? QueryResult;

        public static bool operator ==(in QueryOpResult<T> lhs, in QueryOpResult<T> rhs)
        {
            return lhs.ResultId == rhs.ResultId && lhs.HasQueryRes == rhs.HasQueryRes &&
                               lhs.IsTest == rhs.IsTest && lhs.FaultingException == rhs.FaultingException &&
                               Compare(lhs.QueryResult, rhs.QueryResult);

            static bool Compare(T? l, T? r)
            {
                if (l.HasValue != r.HasValue) return false;
                if (l == null) return true;
                return EqualityComparer<T>.Default.Equals(l.Value, r.Value);
            }
        }

        public static bool operator !=(in QueryOpResult<T> lhs, in QueryOpResult<T> rhs) => !(lhs == rhs);
        public override int GetHashCode() => ResultId.GetHashCode();
        public override bool Equals(object obj) => obj is QueryOpResult<T> res && res == this;
        public bool Equals(QueryOpResult<T> other) => other == this;
        public override string ToString() => GetStringRep();

        private QueryOpResult(bool isTest, Exception faultingException, T? qRes)
        {
            IsTest = isTest;
            FaultingException = faultingException;
            QueryResult = qRes;
            ResultId = Guid.NewGuid();
            _faultingExType = faultingException?.GetType();
        }

        private string GetStringRep()
        {
            string ret;
            if (IsInvalidDefault)
            {
                ret = "INVALID DEFAULT RESULT";
            }
            else
            {
                ret = IsTest ? "TEST RESULT" : "CONTROL RESULT";
                ret += ((_faultingExType == null)
                    ? "\t\tNo Exception Thrown."
                    : ("\t\tException Thrown Of Type - [" + _faultingExType.Name + "]."));
                ret += QueryResult != null
                    ? ("\t\tHas Query result with value: [" + QueryResult.Value + "].")
                    : "\t\tNo Query Result.";
            }
            return ret;
        }

        [CanBeNull] private readonly Type _faultingExType;
    }
}
