using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;

namespace VaultUnitTests
{
    public sealed class DualQueryOperationResult<[VaultSafeTypeParam] TResult> : IDualOperationResult, IEquatable<DualQueryOperationResult<TResult>> 
        where TResult : unmanaged, IEquatable<TResult>, IComparable<TResult>
    {
        public bool ResultsMatch => _match;
        public ref readonly QueryOpResult<TResult> ControlResult => ref _controlResult;
        public ref readonly QueryOpResult<TResult> TestResult => ref _testResult;

        public DualQueryOperationResult(in QueryOpResult<TResult> controlResult, in QueryOpResult<TResult> testResult)
        {
            if (controlResult.IsInvalidDefault) throw new ArgumentException(@"Invalid default value not allowed.", nameof(controlResult));
            if (testResult.IsInvalidDefault) throw new ArgumentException(@"Invalid default value not allowed.", nameof(testResult));
            if (!controlResult.IsControl) throw new ArgumentException(@"Parameter must be a control result.", nameof(controlResult));
            if (!testResult.IsTest) throw new ArgumentException(@"Parameter must be a test result.", nameof(controlResult));

            _controlResult = controlResult;
            _testResult = testResult;
            _match =
                (_controlResult.QueryResult.HasValue == _testResult.QueryResult.HasValue) && 
                ( !_controlResult.QueryResult.HasValue ||  
                  (_testResult.QueryResult.HasValue &&  
                   EqualityComparer<TResult>.Default.Equals(_controlResult.QueryResult.Value, _testResult.QueryResult.Value))) 
                &&
                ((_controlResult.FaultingException == null) == (_testResult.FaultingException == null)) &&

                (_controlResult.FaultingException == null || CompareTypes(_testResult.FaultingException?.GetType(),
                    _controlResult.FaultingException?.GetType()));

            static bool CompareTypes(Type test, Type control)
            {
                if (ReferenceEquals(test, control)) return true;
                if (ReferenceEquals(test, null) || ReferenceEquals(control, null)) return false;

                if (typeof(ArgumentNegativeException).IsAssignableFrom(test) ||
                    typeof(ArgumentNotPositiveException).IsAssignableFrom(test))
                    return typeof(ArgumentOutOfRangeException).IsAssignableFrom(control);

                return test == control;
            }
        }

        public bool Equals(DualQueryOperationResult<TResult> other) => other != null && _match == other._match && other._controlResult == _controlResult && other._testResult == _testResult;

        public override int GetHashCode()
        {
            int hash = _match.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ _controlResult.GetHashCode();
                hash = (hash * 397) ^ _testResult.GetHashCode();
            }
            return hash;
        }

        public bool Equals(IDualOperationResult other) => Equals(other as DualQueryOperationResult<TResult>);

        public override bool Equals(object other) => Equals(other as DualQueryOperationResult<TResult>);

        public static bool operator ==(DualQueryOperationResult<TResult> lhs, DualQueryOperationResult<TResult> rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (ReferenceEquals(lhs, null)) return false;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(DualQueryOperationResult<TResult> lhs, DualQueryOperationResult<TResult> rhs) => !(lhs == rhs);

        public override string ToString()
        {
            string ret = _stringRep;
            if (ret == null)
            {
                string setMe = "Dual operation result: [" + (_match ? "IS MATCH" : "IS NOT MATCH") + "]" + Environment.NewLine + "\t\t" + _controlResult + Environment.NewLine + "\t\t" + _testResult + Environment.NewLine;
                Interlocked.CompareExchange(ref _stringRep, setMe, null);
                ret = _stringRep;
            }
            Debug.Assert(ret != null, "ret != null");
            return ret;
        }


        //Never set outside of ToString Method
        private volatile string _stringRep;

        private readonly bool _match;
        private readonly QueryOpResult<TResult> _controlResult;
        private readonly QueryOpResult<TResult> _testResult;
    }
}