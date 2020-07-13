using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;

namespace VaultUnitTests
{
    public sealed class DualArrayBearingOperationResult<[VaultSafeTypeParam] T> : IDualOperationResult, IEquatable<DualArrayBearingOperationResult<T>> where T : unmanaged, IEquatable<T>, IComparable<T>
    {
        public bool ResultsMatch => _match;
        public ref readonly ArrayBearingResult<T> ControlResult => ref _controlResult;
        public ref readonly ArrayBearingResult<T> TestResult => ref _controlResult;

        public DualArrayBearingOperationResult(in ArrayBearingResult<T> controlResult, in ArrayBearingResult<T> testResult)
        {
            if (controlResult.IsInvalidDefault) throw new ArgumentException(@"Invalid default value not allowed.", nameof(controlResult));
            if (testResult.IsInvalidDefault) throw new ArgumentException(@"Invalid default value not allowed.", nameof(testResult));
            if (!controlResult.IsControl) throw new ArgumentException(@"Parameter must be a control result.", nameof(controlResult));
            if (!testResult.IsTest) throw new ArgumentException(@"Parameter must be a test result.", nameof(controlResult));

            _controlResult = controlResult;
            _testResult = testResult;
            _match =
                (_controlResult.HasArrayResult == _testResult.HasArrayResult &&
                 _controlResult.ResultingArray.SequenceEqual(_testResult.ResultingArray)) &&
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

        public bool Equals(DualArrayBearingOperationResult<T> other) => other != null && _match == other._match && other._controlResult == _controlResult && other._testResult == _testResult;

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

        public bool Equals(IDualOperationResult other) => Equals(other as DualArrayBearingOperationResult<T>);

        public override bool Equals(object other) => Equals(other as DualArrayBearingOperationResult<T>);

        public static bool operator==(DualArrayBearingOperationResult<T> lhs, DualArrayBearingOperationResult<T> rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (ReferenceEquals(lhs, null)) return false;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(DualArrayBearingOperationResult<T> lhs, DualArrayBearingOperationResult<T> rhs) => !(lhs == rhs);


        //Never set outside of ToString Method
        private volatile string _stringRep;

        private readonly bool _match;
        private readonly ArrayBearingResult<T> _controlResult;
        private readonly ArrayBearingResult<T> _testResult;
    }
}